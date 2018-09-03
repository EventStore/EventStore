using System;

namespace EventStore.Common.Utils
{
    public static class Runtime
    {
        public static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;

        public static readonly bool IsUnixOrMac = Environment.OSVersion.Platform == PlatformID.Unix
                                                  | Environment.OSVersion.Platform == PlatformID.MacOSX;

        public static readonly bool IsWindows = !IsUnixOrMac;

        public static readonly bool IsMacOS = Environment.OSVersion.Platform == PlatformID.MacOSX;
    }
}