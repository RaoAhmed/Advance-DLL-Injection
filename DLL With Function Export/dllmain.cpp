// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <Windows.h>
#include <iostream>

// This Struct Should Match The C# Injector Struct
struct SharedData
{
    char source[260];
    char destination[260];
    int fileCount;
};

SharedData* ReadSharedMemory(HANDLE& hMap)
{
    // Attempt to open an existing shared memory object created by another process.
    // FILE_MAP_ALL_ACCESS → allows both read and write access.
    // FALSE → the handle cannot be inherited by child processes.
    // "MySharedMemory" → the name of the shared memory object to open.
    hMap = OpenFileMappingW(
        FILE_MAP_ALL_ACCESS,
        FALSE,
        L"MySharedMemory"
    );

    // Check if the shared memory object was successfully opened.
    if (!hMap)
    {
        // Display an error message if opening fails.
        MessageBoxA(NULL, "OpenFileMapping failed", "DLL With Function Export", MB_OK);

        // Return nullptr to indicate failure.
        return nullptr;
    }

    // Map the shared memory into the current process’s address space.
    // This returns a pointer to the memory region that can be accessed directly.
    SharedData* data = (SharedData*)MapViewOfFile(
        hMap,                   // Handle to the shared memory object
        FILE_MAP_ALL_ACCESS,    // Read/write access
        0,                      // File offset high
        0,                      // File offset low
        sizeof(SharedData)      // Size of the mapping (only the SharedData struct)
    );

    // Verify that the mapping succeeded.
    if (!data)
    {
        // If mapping fails, close the handle and return nullptr.
        CloseHandle(hMap);
        return nullptr;
    }

    // Perform basic validation to ensure the shared memory contains valid data.
    // Check that both 'source' and 'destination' strings are non-empty.
    if (strlen(data->source) == 0 || strlen(data->destination) == 0)
    {
        // Show an error message if the data is invalid.
        MessageBoxA(NULL, "Invalid shared memory data", "DLL ERROR", MB_ICONERROR);

        // Clean up resources before returning.
        UnmapViewOfFile(data);
        CloseHandle(hMap);

        return nullptr;
    }

    // If everything is valid, return the pointer to the shared memory data.
    return data;
}



extern "C" __declspec(dllexport)
int WINAPI MyFunction(int a, int b) // Remote Function
{
    /*std::cout << "This is Remote Function.";
    std::cout << "This line is printed from Remote Function";*/

    HANDLE hMap = nullptr;

    SharedData* params = ReadSharedMemory(hMap);

    if (!params)
    {
        std::cout << "ReadSharedMemory failed" << std::endl;
        return FALSE;
    }

    /*std::cout << "Source: " << params->source << std::endl;
    std::cout << "Destination: " << params->destination << std::endl;*/

    MessageBoxA(NULL, params->source, "Source", MB_OK);
    MessageBoxA(NULL, params->destination, "Destination", MB_OK);

    // Cleanup
    UnmapViewOfFile(params);

    if (hMap)
        CloseHandle(hMap);

    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

