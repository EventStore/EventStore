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
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring.Utils;

namespace EventStore.Core.Services.Monitoring.Stats
{
    public class EsDriveInfo
    {
        public readonly string DiskName;
        public readonly long TotalBytes;
        public readonly string TotalBytesFriendly;
        public readonly long AvailableBytes;
        public readonly string AvailableBytesFriendly;
        public readonly long UsedBytes;
        public readonly string UsedBytesFriendly;
        public readonly string Usage;

        public static EsDriveInfo FromDirectory(string path, ILogger log)
        {
            try
            {
                string driveName;
                if (OS.IsLinux)
                {
                    driveName = GetDirectoryRootInLinux(path, log);
                    if (driveName == null)
                        return null;
                }
                else
                {
                    driveName = Directory.GetDirectoryRoot(path);
                }

                var drive = new DriveInfo(driveName);
                var esDrive = new EsDriveInfo(drive.Name, drive.TotalSize, drive.AvailableFreeSpace);
                return esDrive;
            }
            catch (Exception ex)
            {
                log.Debug("Error while reading drive info for path {0}. Message: {1}.", path, ex.Message);
                return null;
            }
        }

        private EsDriveInfo(string diskName, long totalBytes, long availableBytes)
        {
            DiskName = diskName;
            TotalBytes = totalBytes;
            TotalBytesFriendly = TotalBytes.ToFriendlySizeString();
            AvailableBytes = availableBytes;
            AvailableBytesFriendly = AvailableBytes.ToFriendlySizeString();
            UsedBytes = TotalBytes - AvailableBytes;
            UsedBytesFriendly = UsedBytes.ToFriendlySizeString();
            Usage = TotalBytes != 0
                    ? (UsedBytes * 100 / TotalBytes).ToString(CultureInfo.InvariantCulture) + "%"
                    : "0%";
        }

        private static string GetDirectoryRootInLinux(string directory, ILogger log)
        {
            // http://unix.stackexchange.com/questions/11311/how-do-i-find-on-which-physical-device-a-folder-is-located

            // example

            // Filesystem     1K-blocks      Used Available Use% Mounted on
            // /dev/sda1      153599996 118777100  34822896  78% /media/CC88FD3288FD1C20

            try
            {
                var driveInfo = ShellExecutor.GetOutput("df", string.Format("-P {0}", directory));
                var driveInfoLines = driveInfo.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                var ourline = driveInfoLines[1];
                var spaces = new Regex(@"[\s\t]+", RegexOptions.Compiled);
                var trimmedLine = spaces.Replace(ourline, " ");
                var driveName = trimmedLine.Split(' ')[5]; //we choose the 'mounted on' column
                return driveName;
            }
            catch (Exception ex)
            {
                log.DebugException(ex, "couldn't get drive name for directory {0} on linux", directory);
                return null;
            }
        }
    }
}