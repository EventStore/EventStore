#Event Store

**The documentation has now moved to the <a href="https://github.com/EventStore/EventStore/wiki">wiki in this repository</a>.** For a quick start, look <a href="https://github.com/EventStore/EventStore/wiki/Running-the-Event-Store">here</a>.

<em>**Development is on the "dev" branch (and feature branches). Please make any pull requests to the "dev" branch**.</em>

This is the repository for the open source version of Event Store, which now includes the clustering implementation for high availability. Further information on commercial support and options such as LDAP authentication can be found on the Event Store website at http://geteventstore.com.

##Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the v8 JavaScript engine), it must be built for the platform on which you intend to run it.

Binaries are available from http://geteventstore.com, however if you want to build it from source, instructions for Windows and Linux are below.

###Debug Builds on Windows / .NET

####Prerequisites

- .NET Framework v4.0+
- Windows platform SDK with compilers (v7.1) or Visual C++ installed
- git on PATH
- svn on PATH

####Building the Event Store

*Important note: if you have previously built from source, it's possible you have V8 checked out via git instead of Subversion. If this is the case, you should use the `clean-all` target noted below before building again.*

From a command prompt:

- `build.cmd` - runs the Event Store build
- `build.cmd clean-all` - cleans the repository

Optional parameters (Specified using `-ParameterName value`)

- `-Platform` - `x64` (default) or `x86`
- `-Configuration` - `release` (default) or `debug`
- `-Version` - the semantic version number to give to the release. Defaults to version `0.0.0.0` which should be used for all non-released builds.
- `-SpecificVisualStudioVersion` - `2010`, `2012`, `2013`, `Windows7.1SDK`. Default is to use whichever version is installed - this only needs to be overriden if you have multiple versions installed.
- `-ForceNetwork` - true if you want to force the script to get dependencies even if Windows thinks theres no network connection (otherwise we don't try to avoid sometimes lengthy delays).
- `-Defines` - any additional defines you want to pass to the compiler. Should be enclosed in single quotes

####Building the Event Store from Visual Studio

If you want to build from Visual Studio, it's necessary to first build from the
command line in order to build `js1.dll` which incorporates V8. When this is
available in the `src\EventStore\Libs\` directory, it is possible to build the
`src\EventStore\EventStore.sln` solution from within Visual Studio.

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
- Mono version 3.2.3 or greater on PATH
- svn on `PATH`
- gcc installed

####Building the Event Store

```bash
./build.sh <target> <version> <platform> <configuration>
```

- `target` is one of `quick`, `incremental` or `full` (see above)
- `version` is the semantic version to apply
- `platform` - either x86 or x64 (defaults to x64)
- `configuration` - either debug or release (defaults to release)