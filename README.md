# ADVANCE DLL INJECTION
This project demonstrates:
* How to inject a DLL into another process
* Calling a remote funtion
* Passing Data between processes

## REPOSITORY STRUCTURE
This repository cantains two directories:
- ### Advance DLL Injector:
This is a Visual Studio project that contains the code for the Advanced DLL Injector. The code is in the Program.cs file. It injects the DLL, calls the remote function, **MyFunction**, and passes data to the remote function through shared memory.
- ### DLL With Function Export
This is a separate Visual Studio project (independent from the Advanced DLL Injector project) that contains the DLL code. The DLL exports the function named **MyFunction** and implements reading from shared memory.

### HOW TO RUN THIS PROJECT
follow these steps to run the project:
* **Step 1.** Build both projects (Advanced DLL Injector & DLL with Function Export) separately. Make sure to build both projects with the same architecture.
* **Step 2.** Place the built **DLL with Function Export.dll** file in the same folder as the **Advanced DLL Injector.exe** file.
* **Step 3.** Open the Command Prompt in the folder where **Advanced DLL Injector.exe** is located and run it through CMD.
* **OUTPUT:** **Notepad** will open, and two message boxes will appear one after the other.
