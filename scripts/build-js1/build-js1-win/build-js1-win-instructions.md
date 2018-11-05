

## Windows VM setup
Set up a VM with `Microsoft Windows Server 2016 Base` as OS and 50GB of storage space

## Building Google v8's embedded library (v8_monolith.lib)
The instructions below are based on [steps to build Chromium](https://chromium.googlesource.com/chromium/src/+/master/docs/windows_build_instructions.md
)

### Visual Studio 2017 Components
1. Download & Run Visual Studio 2017 installer
2. Install the following components
- `Desktop development with C++`
- Make sure `Visual C++ ATL for x86 and x64` and `Visual C++ MFC for x86 and x64` are also installed
3. Make sure `Windows 10 SDK version 10.0.17134` is installed.
4. Install `Debugging Tools for Windows`:
```
Control Panel → Programs → Programs and Features → Select the “Windows Software Development Kit” → Change → Change → Check “Debugging Tools For Windows” → Change
```

### Depot Tools
1. Download [Depot Tools](https://storage.googleapis.com/chrome-infra/depot_tools.zip)
2. Extract to `C:\src\depot_tools`
3. Add `C:\src\depot_tools` to your `PATH` environment variable (add it at the beginning to make sure it has precedence over other paths)
4. Add `DEPOT_TOOLS_WIN_TOOLCHAIN` environment variable with value `0`

### Git Configuration
Configure git as follows from the command prompt:
```console
> git config --global user.name "My Name"
> git config --global user.email "my-name@chromium.org"
> git config --global core.autocrlf false
> git config --global core.filemode false
> git config --global branch.autosetuprebase always
```

### Fetch and compile v8:
Execute the following from a command prompt:
```console
> mkdir v8 && cd v8
> fetch v8
> cd v8
> git checkout branch-heads/7.0
> python ./tools/dev/v8gen.py -vv x64.release -- v8_monolithic=true v8_use_external_startup_data=false use_cxx11=true use_pic=true is_clang=false treat_warnings_as_errors=true
> ninja -C .\out.gn\x64.release v8_monolith
```

After compilation, verify that a file named `v8_monolith.lib` is present under `out.gn\x64.release\obj\`

### Building `js1.dll`

1. Install the following additional components using the Visual Studio 2017 installer:
- `Windows 8.1 SDK`
- `Windows Universal CRT SDK`
2. Clone the EventStore repository
```console
> git clone https://github.com/EventStore/EventStore.git
```
3. Open the `EventStore.Projections.v8Integration.sln` project file located under `src/EventStore.Projections.v8Integration` with Microsoft Visual Studio 2017
4. Select the Build Configuration: `x64` / `release`
5. Change the following under the project's properties:
- `General` - Make sure the target platform version is set to `Windows 8.1`
- `VC++ Directories` → `Include Directories` - Change the path to your local `v8\include` path
- `Linker` → `General` → `Additional Library Directories` - Change the path to your local `v8\out.gn\x64.release\obj` path
- `Linker` → `Input` - Make sure the following libraries are included:  `v8_monolith.lib`, `DbgHelp.lib`, `winmm.lib`, `Shlwapi.lib`

6. Build the solution

This should generate `js1.dll` DLL under `src\libs\x64\win\`