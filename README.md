#Event Store

This is the repository for the open source version of Event Store. Binaries, documentation and information about the commercial, multi-node version can be found on the Event Store website at http://geteventstore.com.

##Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the v8 JavaScript engine), it must be built for the platform on which you intend to run it. Binaries are available from http://geteventstore.com, however if you want to build it from source, instructions for Windows and Linux are below. 

###Debug Builds on Windows / .NET

####Prerequisites

	- Visual Studio 2010 (with .NET 4 and 64-bit C++ support)
	- git on PATH
	- svn on PATH

####Environment

Either use a Visual Studio 2010 x64 Command Prompt, or run

	"C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat" x64

####Download and build v8

	src\EventStore\Scripts\v8\get-v8.cmd 

	src\EventStore\Scripts\v8\build-v8_x64.cmd 

####Build the v8 integration code

	C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe /p:Configuration=Debug;Platform=x64 src\EventStore\Projections.Dev.WindowsOnly.sln 

This step produces a file named js1.dll, which contains the projections framework. If you already have access to a suitable version of this file (e.g. from the binary distribution) you can proceed to step 4, having made it available in src\EventStore\libs\x64.

####Build the Event Store solution using 64-bit msbuild

	C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe /p:Configuration=Debug;Platform=x64 src\EventStore\EventStore.sln

<em>NOTE:

EventStore.sln platform mapping are configured to be compatible with both xbuild on mono and msbuild in .NET.

The "Any CPU" platform in the EventStore.sln solution is actually x64, but due to build problems in mono it has to be "Any CPU"

Projections.Dev.WindowsOnly.sln platform mappings are configured to be used in Windows only.  These settings are compatible with ContinuousTests and R# (unit test runner starts 64-bit runner and loads js1.dll corectly)</em>

###Debug Builds on Linux (Ubuntu 12.04) / Mono

####Prerequisites

- Patched version of Mono on PATH
- svn on PATH

You can get and build the patched version of Mono necessary for Event Store by running

	.\src\EventStore\Scripts\get-mono-303p.sh

This script will install mono to /opt/mono, and must be run with root priviledges (since it installs packages via apt-get).

####Download and build v8 

	./src/EventStore/Scripts/v8/get-v8.sh 
	
	./src/EventStore/Scripts/v8/build-v8.sh 

####Build the v8 integration code (libjs1.so)

	./src/EventStore/Scripts/v8/build-js1.sh 

####Build the Event Store Solution

The Event Store solution can be build using either MonoDevelop or xbuild.

	/opt/mono/bin/xbuild src/EventStore/EventStore.sln /p:Configuration=Debug /p:Platform="Any CPU"
