// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Reflection;
using EventStore.Common.Log;

namespace EventStore.Common.Utils
{
    public enum OsFlavor
    {
        Unknown,
        Windows,
        Linux,
        BSD,
        MacOS
    }

    public class OS
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<OS>();

        public static readonly OsFlavor OsFlavor = DetermineOSFlavor();

        public static bool IsUnix
        {
            get
            {
                var platform = (int)Environment.OSVersion.Platform;
                return (platform == 4) || (platform == 6) || (platform == 128);
            }
        }

        public static string GetHomeFolder()
        {
            return IsUnix
                       ? Environment.GetEnvironmentVariable("HOME")
                       : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
        }

        public static string GetRuntimeVersion()
        {
            var type = Type.GetType("Mono.Runtime");
            if (type != null)
            {
                MethodInfo getDisplayNameMethod = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
                return getDisplayNameMethod != null ? (string)getDisplayNameMethod.Invoke(null, null) : "Mono <UNKNOWN>";
            }
            // must be .NET
            return ".NET " + Environment.Version;
        }

        private static OsFlavor DetermineOSFlavor()
        {
            if (!IsUnix) // assume Windows
                return OsFlavor.Windows;

            string uname = null;
            try
            {
                uname = ShellExecutor.GetOutput("uname", "");
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Couldn't determine the flavor of Unix-like OS.");
            }

            switch (uname)
            {
                case "Linux":
                    return OsFlavor.Linux;
                case "Darwin":
                    return OsFlavor.MacOS;
                case "FreeBSD":
                case "NetBSD":
                case "OpenBSD":
                    return OsFlavor.BSD;
                default:
                    return OsFlavor.Unknown;
            }
        }
    }
}