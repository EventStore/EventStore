#Event Store

**The documentation has now moved to the <a href="https://github.com/EventStore/EventStore/wiki">wiki in this repository</a>.** For a quick start, look <a href="https://github.com/EventStore/EventStore/wiki/Running-the-Event-Store">here</a>.

<em><strong>Development is on the "dev" branch (and feature branches). Please make any pull requests to the "dev" branch.</strong></em>

This is the repository for the open source version of Event Store. Binaries and information about the commercial, multi-node version can be found on the Event Store website at http://geteventstore.com.

##Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the v8 JavaScript engine), it must be built for the platform on which you intend to run it. Binaries are available from http://geteventstore.com, however if you want to build it from source, instructions for Windows and Linux are below. 

###Debug Builds on Windows / .NET

####Prerequisites

	- Visual Studio 2010 or 2012 (with .NET 4 and 64-bit C++ support)
	- git on PATH
	- svn on PATH

####Download and build v8

	src\EventStore\Scripts\v8\get-v8.cmd 

	src\EventStore\Scripts\v8\build-v8_x64_release.cmd 

####Build the v8 integration code

	src\EventStore\Scripts\v8\build-js1_x64_release.cmd 

This step produces a file named js1.dll, which contains the projections framework. If you already have access to a suitable version of this file (e.g. from the binary distribution) you can proceed to step 4, having made it available in src\EventStore\libs\x64.

####Build the Event Store solution using 64-bit msbuild

	C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe /p:Configuration=Debug;Platform="Any CPU" src\EventStore\EventStore.sln

*NOTE: EventStore.sln has build configurations set up to be compatible with either xbuild or msbuild. Although named "Any CPU", it in fact targets x64 only.*

###Debug Builds on Linux (Ubuntu 12.04) / Mono

####Prerequisites

- Patched version of Mono on `PATH`
- svn on `PATH`

You can get and build the patched version of Mono necessary for Event Store by running

	.\src\EventStore\Scripts\get-mono-307p.sh

This script will install mono to `/opt/mono`, and must be run with root priviledges (since it installs packages via apt-get). However, the script will not add it to the `PATH` which must be done separately, such that `mono --version` outputs:

<pre>
Mono JIT compiler version (EventStore patched build: ThreadPool.c) 3.0.7 ((no/514fcd7 Fri Mar 15 14:49:41 GMT 2013) (EventStore build)
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

####Download and build v8 

	./src/EventStore/Scripts/v8/get-v8.sh 
	
	./src/EventStore/Scripts/v8/build-v8_x64_release.sh 

####Build the v8 integration code (libjs1.so)

	./src/EventStore/Scripts/v8/build-js1.sh 

####Build the Event Store Solution

The Event Store solution can be build using either MonoDevelop or xbuild.

	/opt/mono/bin/xbuild src/EventStore/EventStore.sln /p:Configuration=Debug /p:Platform="Any CPU"
