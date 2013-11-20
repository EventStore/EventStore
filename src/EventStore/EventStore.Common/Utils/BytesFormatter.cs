﻿// Copyright (c) 2012, Event Store LLP
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
using System.Collections.Generic;
using System.Globalization;

namespace EventStore.Common.Utils
{
    public static class BytesFormatter
    {
        private static readonly string[] SizeOrders = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        private static readonly string[] SpeedOrders = new[] { "B/s", "KiB/s", "MiB/s", "GiB/s", "TiB/s" };
        private static readonly string[] NumberOrders = new[] { "", "K", "M", "G", "T" };

        public static string ToFriendlySpeedString(this double bytes)
        {
            return FormatSpeed((float) bytes);
        }

        public static string ToFriendlySizeString(this ulong bytes)
        {
            return bytes > long.MaxValue ? "more then long.MaxValue" : ToFriendlySizeString((long) bytes);
        }
        public static string ToFriendlySizeString(this long bytes)
        {
            return FormatLong(bytes, SizeOrders);
        }

        public static string ToFriendlyNumberString(this ulong number)
        {
            return number > long.MaxValue ? "more then long.MaxValue" : ToFriendlyNumberString((long)number);
        }
        public static string ToFriendlyNumberString(this long number)
        {
            return FormatLong(number, NumberOrders);
        }

        private static string FormatLong(long bytes, IEnumerable<string> orders)
        {
            const int scale = 1024;
            bool isNegative = bytes < 0;
            bytes = Math.Abs(bytes);

            long max = 1;
            string finalOrder = string.Empty;

            foreach (var order in orders)
            {
                max *= scale;
                finalOrder = order;
                if (bytes < max)
                    break;
            }
            max /= scale;

            var friendlyStr = string.Format(CultureInfo.InvariantCulture,
                                            "{0}{1:##0.##}{2}",
                                            isNegative ? "-" : string.Empty,
                                            bytes*1.0/(double)max,
                                            finalOrder);
            return friendlyStr;
        }

        //the only difference is double vs long
        private static string FormatSpeed(double bytesPerSec)
        {
            const int scale = 1024;
            bool isNegative = bytesPerSec < 0; // verrry strange, but we need to show this if it happened already
            bytesPerSec = Math.Abs(bytesPerSec);
            
            long max = 1;
            string finalOrder = string.Empty;

            foreach (var speedOrder in SpeedOrders)
            {
                max *= scale;
                finalOrder = speedOrder;
                if (bytesPerSec < max)
                    break;
            }
            max /= scale;

            var friendlyStr = string.Format(CultureInfo.InvariantCulture,
                                            "{0}{1:##0.##}{2}",
                                            isNegative ? "-" : string.Empty,
                                            bytesPerSec/(double) max,
                                            finalOrder);
            return friendlyStr;
        }
    }
}
