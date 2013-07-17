#Event Store

**The documentation has now moved to the <a href="https://github.com/EventStore/EventStore/wiki">wiki in this repository</a>.** For a quick start, look <a href="https://github.com/EventStore/EventStore/wiki/Running-the-Event-Store">here</a>.

<em>**Development is on the "dev" branch (and feature branches). Please make any pull requests to the "dev" branch**.</em>

This is the repository for the open source version of Event Store. Binaries and information about the commercial, multi-node version can be found on the Event Store website at http://geteventstore.com.

##Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the v8 JavaScript engine), it must be built for the platform on which you intend to run it. Binaries are available from http://geteventstore.com, however if you want to build it from source, instructions for Windows and Linux are below. 

###Debug Builds on Windows / .NET

####Prerequisites

- .NET Framework v4.0+
- Windows platform SDK with compilers (v7.1) or Visual C++ installed
- git on PATH
- svn on PATH

####Building the Event Store

From a command prompt or powershell:

- `psake.cmd Build-Quick` - only builds the Event Store, fails if V8 and JS1 aren't available
- `psake.cmd Build-Incremental` - will build V8 if necessary, JS1 if necessary and Event Store always
- `psake.cmd Build-Full` - cleans and builds everything

Optional parameters (passed in the -parameters @{} hash):

- `platform` - x86 or x64 (defaults to x64)
- `configuration` - release or debug (defaults to release)
- `version` - the semantic version number to give to the release (used only in the release pipeline, CI and nightlies default to 0.0.0.0 but still have the branch/commit hash embedded in them).
- `platformToolset` - C++ toolset to use - v110, v100, WindowsSDK7.1 (defaults to the latest we can guess at)
- `forceNetwork` - true if you want to force the script to get dependencies even if Windows thinks theres no network connection (otherwise we don't try to avoid sometimes lengthy delays).

####Building the Event Store from Visual Studio

If you want to build from Visual Studio, it's necessary to first build from the
command line in order to build `js1.dll` which incorporates V8. When this is
available in the `src\EventStore\Libs\` directory, it is possible to build the
`src\EventStore\EventStore.sln` solution from within Visual Studio (this is
largely speaking the same as using the psake build script in "Quick" mode).

When building through Visual Studio, there are PowerShell scripts which run as
pre- and post-build tasks on the EventStore.Common project, which set the
informational version attribute of the EventStore.Common.dll assembly to the
current commit hash on each build and then revert it.

Unfortunately Visual Studio runs these scripts in 32-bit PowerShell. Since it's
most likely that you're running 64-bit PowerShell under normal circumstances,
the execution policy of 32-bit PowerShell will probably prohibit running
scripts. *There is a batch file in the root of the repository named
`RunMeElevatedFirst.cmd` which will set the execution policy for 32-bit
PowerShell if you run it as Administrator. Obviously you may want to audit what
the script does before executing it on your machine!*

###Debug Builds on Linux (Ubuntu 12.04) / Mono

####Prerequisites

- git on `PATH`
- Patched version of Mono on `PATH` (see below)
- svn on `PATH`
- gcc installed

####Building Patched Mono

You can get and build the patched version of Mono necessary for Event Store by running

	./src/EventStore/Scripts/get-mono-3012p.sh

This script will install mono to `/opt/mono`, and must be run with root priviledges (since it installs packages via apt-get). However, the script will not add it to the `PATH` which must be done separately, such that `mono --version` outputs:

<pre>
Mono JIT compiler version (EventStore patched build: ThreadPool.c) 3.0.12 ((no/514fcd7 Fri Mar 15 14:49:41 GMT 2013) (EventStore build)
Copyright (C) 2002-2012 Novell, Inc, Xamarin Inc and Contributors. www.mono-project.com
        TLS:           __thread
        SIGSEGV:       altstack
        Notifications: epoll
        Architecture:  amd64
        Disabled:      none
        Misc:          softdebug
        LLVM:          supported, not enabled.
        GC:            Included Boehm (with typed GC and Parallel Mark)
</pre>

####Building the Event Store

```bash
./build.sh <mode> <version> <platform> <configuration>
```

- `mode` is one of `quick`, `incremental` or `full` (see above)
- `version` is the semantic version to apply
- `platform` - either x86 or x64 (defaults to x64)
- `configuration` - either debug or release (defaults to release)
