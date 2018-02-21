# Event Store

The open-source, functional database with Complex Event Processing in JavaScript.

This is the repository for the open source version of Event Store, which includes the clustering implementation for high availability. 

## Support

Information on commercial support and options such as LDAP authentication can be found on the Event Store website at https://eventstore.org/support.

## CI Status

| OS | Status |
|------:|--------:|
|**Ubuntu 14.04**|[![Build status](https://app.wercker.com/status/efbd313efd4406243ca7b6688ddbc286/s/release-v4.0.0 "wercker status")](https://app.wercker.com/project/byKey/efbd313efd4406243ca7b6688ddbc286)|
|**Windows 8.1**|[![Build status](https://ci.appveyor.com/api/projects/status/rpg0xvt6facomw0b?svg=true)](https://ci.appveyor.com/project/EventStore/eventstore-aasj1)|

## Documentation
Documentation for Event Store can be found [here](http://docs.geteventstore.com/)

## Community
We have a fairly active [google groups list](https://groups.google.com/forum/#!forum/event-store). If you prefer slack, there is also an #eventstore channel [here](http://ddd-cqrs-es.herokuapp.com/).

## Release Packages
The latest release packages are hosted in the downloads section on the [Event Store Website](https://eventstore.org/downloads/)

We also host native packages for Linux on [Package Cloud](https://packagecloud.io/EventStore/EventStore-OSS) and Windows packages can be installed via [Chocolatey](https://chocolatey.org/packages/eventstore-oss) (4.0.0 onwards only).

## Building Event Store

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the V8 JavaScript engine), it must be built for the platform on which you intend to run it.

### Linux
**Prerequisites**
- Mono 4.6.2

**Example**

`./build.sh {version} {configuration} {platform_override}`

**Parameters**
- **Version**: Assembly Version (Required to conform to major[.minor[.build[.revision]]])
- **Configuration**: Build Configuration (debug or release)
- **Platform Override**:
When Event Store is building, the build script will attempt to locate the projections library (libjs1.so) that was built for the particular platform the build script is running on.
The repository has a couple of prebuilt projections libraries for various platforms such as Ubuntu-14.04, Ubuntu-16.04 and CentOS 7.2.1511.
In some cases you won't need to recompile the library for the particular platform but a compatible one might be available. In these cases, the platform override can be used to let the build script know that you are happy to use one of the prebuilt ones by specifying the one it should use.

The list of precompiled projections libraries can be found in `src/libs/x64`

### Windows
**Prerequisites**
- .NET Framework 4.0+
- Windows platform SDK with compilers (v7.1) or Visual C++ installed (Only required for a full build)

**Example**

`.\build.cmd {what-to-build} -Configuration {configuration} -Version {version}`

**Parameters**
- **What to build**: Specify what to build (quick, full, js1)
- **Version**: Assembly Version (Required to conform to major[.minor[.build[.revision]]])
- **Configuration**: Build Configuration (debug or release)

## Building the Projections Library

### Linux
**Prerequisites**
- git

The scripts attempts to do it's best to have no dependencies from the user's point of view. If you do encounter any issues, please don't hesitate to raise an issue.

**Example**

`./scripts/build-js1/build-js1-linux.sh {llvm-download-location} {gyp_flags}`

**Parameters**
- **LLVM Download location**: The script will by default attempt to download the llvm toolchain for the OS it's running on. There can be a case where the script fails to get the correct version of the LLVM toolchain and in those cases, the user can provide the script with the download location for the toolchain.
- **GYP Flags**: In some cases, the user might want to provide their own GYP_FLAGS. One such case is where the user runs into the below mentioned issue. In that case this parameter can be used to pass the appropriate GYP flags.

**Notes**

If the scripts fails with "Failed to download LLVM. You can supply the URL for the LLVM compiler for your OS as the first argument to this script.", it is necessary to provide the script with the location where the LLVM compiler can be downloaded.

If the script fails with an gold linker error such as "ld.gold: error: /usr/lib/gcc/x86_64-linux-gnu/5.4.0/../../../x86_64-linux-gnu/crti.o: unsupported reloc 42 against global symbol __gmon_start__" you can let it use the system linker by specifying -Dlinux_use_bundled_gold=0 as the second parameter to the script.

e.g.

`scripts/build-js1/build-js1-linux.sh "" "-Dlinux_use_bundled_gold=0"`

### Windows
**Prerequisites**
- git
- Windows platform SDK with compilers (v7.1) or Visual C++ installed

**Example**

`.\build.cmd js1`

## Contributing

Development is on the branch aimed at the next release (usually prefixed with `release-v0.0.0`). The `master` branch always contains the code for the latest stable release.

We attempt to do our best to ensure that the history remains clean and to do so, we generally ask contributors to squash their commits into a set or single logical commit.

## Known Issues

### Regressions with Mono 3.12.1 and some versions of the Linux Kernel

*Note: Event Store 4.0.0 onwards is using a more recent version of Mono (4.6.2 at the time of writing). The information below only applies to Event Store <= 3.9.3.*

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
- `>= 4.4.27`

*Note: Please feel free to contribute to this list if you are working on a known good version of the kernel that is not listed here.*
