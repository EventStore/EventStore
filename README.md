
#Event Store

**The documentation has now moved to the <a href="https://github.com/EventStore/EventStore/wiki">wiki in this repository</a>.** For a quick start, look <a href="https://github.com/EventStore/EventStore/wiki/Running-the-Event-Store">here</a>.

<em>**Development is on the branch aimed at the next release (usually prefixed with release-v0.0.0). Please make any pull requests to this branch**.</em>

This is the repository for the open source version of Event Store, which now includes the clustering implementation for high availability. Further information on commercial support and options such as LDAP authentication can be found on the Event Store website at http://geteventstore.com.

##Building from Source

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the v8 JavaScript engine), it must be built for the platform on which you intend to run it.

Binaries are available from http://geteventstore.com, however if you want to build it from source, instructions for Windows and Linux are below.

**Note: We include a precompiled js1 (We include builds of js1 for osx, ubunty-trusty as well as windows). This means that for the majority of cases, v8 does not need to be built from source.**

###Debug Builds on Windows / .NET

####Prerequisites

- .NET Framework v4.0+
- Windows platform SDK with compilers (v7.1) or Visual C++ installed *(Only required for a full build)*
- git on PATH
- svn on PATH *(Only required for a full build)*

####Building the Event Store

From a command prompt:

- `build.cmd` - runs the Event Store build
- `build.cmd clean-all` - cleans the repository

Optional parameters (Specified using `-ParameterName value`)

- `-Platform` - x64
- `-Configuration` - `release` (default) or `debug`
- `-Version` - the semantic version number to give to the release. Defaults to version `0.0.0.0` which should be used for all non-released builds.
- `-SpecificVisualStudioVersion` - `2010`, `2012`, `2013`, `Windows7.1SDK`. Default is to use whichever version is installed - this only needs to be overriden if you have multiple versions installed.
- `-ForceNetwork` - true if you want to force the script to get dependencies even if Windows thinks theres no network connection (otherwise we don't try to avoid sometimes lengthy delays).
- `-Defines` - any additional defines you want to pass to the compiler. Should be enclosed in single quotes

####Building the Event Store from Visual Studio

When building through Visual Studio, there are PowerShell scripts which run as
pre- and post-build tasks on the EventStore.Common project, which set the
informational version attribute of the EventStore.Common.dll assembly to the
current commit hash on each build and then revert it.

###Debug Builds on Linux (Ubuntu 14.04) or MacOS X / Mono

####Prerequisites

- git on `PATH`
- Mono version 3.12.1 or greater on PATH
- svn on `PATH` *(Only required for a full build)*
- gcc installed *(Only required for a full build)*

####Building the Event Store

```bash
./build.sh <target> <version> <platform> <configuration>
```

- `target` is one of `quick`, `incremental` or `full` (see above)
- `version` is the semantic version to apply
- `platform` - always x64
- `configuration` - either debug or release (defaults to release)

On OS X you should use 

```bash
./build.sh <target> <version> x64 <configuration> no-werror
```

To work around warnings that have turned errors.
