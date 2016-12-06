
# Event Store

[![Build status](https://ci.appveyor.com/api/projects/status/rpg0xvt6facomw0b?svg=true)](https://ci.appveyor.com/project/EventStore/eventstore-aasj1)
[![Build status](https://app.wercker.com/status/efbd313efd4406243ca7b6688ddbc286/s/release-v4.0.0 "wercker status")](https://app.wercker.com/project/byKey/efbd313efd4406243ca7b6688ddbc286)

**Documentation is available at http://docs.geteventstore.com.**

***Development is on the branch aimed at the next release (usually prefixed with release-v0.0.0). Please make any pull requests to this branch.***

This is the repository for the open source version of Event Store, which includes the clustering implementation for high availability. Information on commercial support and options such as LDAP authentication can be found on the Event Store website at https://geteventstore.com/support.

## Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the V8 JavaScript engine), it must be built for the platform on which you intend to run it.

Binaries are available from https://geteventstore.com, however if you want to build it from source, instructions for Windows and Linux are below.

### Debug Builds on Linux or Mac OS X

There are two stages to building Event Store. First, a native library used for projections, `libjs1` must be built. Following that, the main Event Store project can be built.

If you are running on Mac OS X Yosemite, Ubuntu Linux 14.04 or Amazon Linux 2015.03, it is not necessary to build `libjs1` from source. Precompiled binaries are already included in this repository. If you are running a different distribution or version than those listed above, you will need to compile `libjs1` yourself.

#### Compiling libjs1

#####Prerequisites

- git on `PATH`
- svn on `PATH`
- gcc installed

##### Instructions (Mac OS X)

From the root of the repository:

```bash
scripts/build-js1/build-js1-mac.sh
```

##### Instructions (Linux)

From the root of the repository:

```bash
scripts/build-js1/build-js1-linux.sh [werror=no]
```

It may be necessary to include `werror=no` as the only parameter to the script if you have a newer compiler which treats warnings appearing as a result of compiling the Google V8 codebase as errors.

#### Compiling Event Store (Linux and Mac OS X)

From the root of the repository:

```bash
./build.sh [<version=0.0.0.0>] [<configuration=release>] [<distro-platform-override>]
```

Versions must be complete four part identifiers valid for use on a .NET assembly.

Valid configurations are:

- debug
- release

The OS distribution and version will be detected automatically unless it is overridden as the last argument. This script expects to find `libjs1.[so|dylib]` in the `src/libs/x64/distroname-distroversion/` directory, built using the scripts in the `scripts/build-js1/` directory. Note that overriding this may result in crashes using Event Store.

*The only supported Linux for production use at the moment is Ubuntu 14.04 LTS.* However, since several people have asked for builds compatible with Amazon Linux in particular, we have included a pre-built version of `libjs1.so` which will link to the correct version of libc on Amazon Linux 2015.03.

Currently the supported versions without needing to build `libjs1` from source are:

- ubuntu-14.04 (Ubuntu Trusty)
- amazon-2015.03 (Amazon Linux 2015.03)

Note that it is no longer possible to build x86 builds of Event Store.

### Debug Builds on Windows / .NET

#### Prerequisites

- .NET Framework v4.0+
- Windows platform SDK with compilers (v7.1) or Visual C++ installed *(Only required for a full build)*
- git on PATH
- svn on PATH *(Only required for a full build)*

#### Building the Event Store

From a command prompt:

- `build.cmd` — runs the Event Store build
- `build.cmd clean-all` — cleans the repository

Optional parameters (Specified using `-ParameterName value`)

- `-Platform` — `x64` (default) or `x86`
- `-Configuration` — `release` (default) or `debug`
- `-Version` — the semantic version number to give to the release. Defaults to version `0.0.0.0`, which should be used for all non-released builds.
- `-SpecificVisualStudioVersion` — `2010`, `2012`, `2013`, `Windows7.1SDK`. Default is to use whichever version is installed. This only needs to be overridden if you have multiple versions installed.
- `-ForceNetwork` — true if you want to force the script to get dependencies even if Windows thinks theres no network connection (otherwise we don’t try to avoid sometimes lengthy delays).
- `-Defines` — any additional defines you want to pass to the compiler. Should be enclosed in single quotes

#### Building the Event Store from Visual Studio

When building through Visual Studio, there are PowerShell scripts which run as pre- and post-build tasks on the EventStore.Common project, which set the informational version attribute of the EventStore.Common.dll assembly to the current commit hash on each build and then revert it.

## Known Issues

### Regressions with Mono 3.12.1 and some versions of the Linux Kernel

There is a known issue with some versions of the Linux Kernel and Mono 3.12.1 which has been raised as an issue [https://bugs.launchpad.net/ubuntu/+source/linux/+bug/1450584](here).
The issue manifests itself as a `NullReferenceException` and the stack trace generally looks like the following.
```
System.NullReferenceException: Object reference not set to an instance of an object
at EventStore.Core.Services.TimerService.ThreadBasedScheduler.DoTiming () [0x00000] in :0
at System.Threading.Thread.StartInternal () [0x00000] in :0
```
Some known good versions of the Kernel are

- 3.13.0-54
- 3.13.0-63
- 3.16.0-39
- 3.19.0-20
- 3.19.0-64
- 3.19.0-66
- >= 4.4.27

*Note: Please feel free to contribute to this list if you are working on a known good version of the kernel that is not listed here.*