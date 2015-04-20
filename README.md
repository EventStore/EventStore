# Event Store

- [Documentation](http://docs.geteventstore.com)
- [Download Binaries](https://geteventstore.com/downloads)
- [Quick Start](http://docs.geteventstore.com/server/latest)

## About

This is the repository for the open source version of Event Store, which now includes the clustering implementation for high availability. Further information on commercial support and options such as LDAP authentication can be found on the Event Store website at https://geteventstore.com.

**Development is on the “dev” branch (and feature branches). Please make any pull requests to the “dev” branch.**

## Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the V8 JavaScript engine) it must be built for the platform on which you intend to run it.

Binaries are available from https://geteventstore.com/downloads, however if you want to build it from source instructions for Windows and Linux are below.

### Debug Builds on Windows / .NET

#### Prerequisites

- .NET Framework 4.0+
- Windows platform SDK with compilers (v7.1) or Visual C++ installed
- Git on `PATH`

#### Building Event Store

If you have previously built from source it’s possible you have V8 checked out via Git instead of Subversion. If this is the case you should use the `clean-all` target noted below before building again.

##### From a command prompt

Command               | Description
:-------------------- | :----------
`build.cmd`           | Runs the Event Store build
`build.cmd clean-all` | Cleans the repository

##### Optional parameters (Specified using `-ParameterName value`)

Argument                       | Values
:----------------------------- | :-----
`-Platform`                    | `x64` (default) or `x86`
`-Configuration`               | `debug` or `release` (default)
`-Version`                     | Semantic version number to give to the release.<br>Defaults to `0.0.0.0` which should be used for<br>all non-released builds.
`-SpecificVisualStudioVersion` | `2010`, `2012`, `2013` or `Windows7.1SDK`.<br>Defaults to whichever version is installed. This<br>only needs to be overriden if you have multiple<br>versions installed.
`-ForceNetwork`                | `true` if you want to force the script to get<br>dependencies even if Windows thinks there’s<br>no network connection (otherwise we don’t try to<br>avoid sometimes lengthy delays).
`-Defines`                     | Any additional defines you want to pass to<br>the compiler. Should be enclosed in single quotes.

#### Building Event Store from Visual Studio

If you want to build from Visual Studio it’s necessary to first build from the command line in order to build `js1.dll` which incorporates V8. When this is available in the `src\EventStore\Libs\` directory it is possible to build the `src\EventStore\EventStore.sln` solution from within Visual Studio.

When building through Visual Studio there are PowerShell scripts which run as pre- and post-build tasks on the EventStore.Common project, which set the informational version attribute of the EventStore.Common.dll assembly to the current commit hash on each build and then revert it.

Unfortunately Visual Studio runs these scripts in 32-bit PowerShell. Since it’s most likely that you’re running 64-bit PowerShell under normal circumstances the execution policy of 32-bit PowerShell will probably prohibit running scripts.

*There is a batch file in the root of the repository named `RunMeElevatedFirst.cmd` which will set the execution policy for 32-bit PowerShell if you run it as Administrator. Obviously you may want to audit what the script does before executing it on your machine!*

### Debug Builds on Linux (Ubuntu 12.04) or MacOS X / Mono

#### Prerequisites

- Git on `PATH`
- Mono version 3.2.3 development packages on `PATH`, and registered with `pkg-config`, such that `which xbuild` and `pkg-config --cflags monosgen-2` return a zero return codes.
- SVN on `PATH`
- GCC installed

#### Building Event Store

```bash
./build.sh <target> <version> <platform> <configuration>
```

Arguments       | Values
:-------------- | :-----
`target`        | `quick`, `incremental` or `full`
`version`       | Semantic version number to give to the release.
`platform`      | `x64` (default) or `x86`
`configuration` | `debug` or `release` (default)
