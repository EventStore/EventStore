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
using System.Text;

namespace EventStore.ClientAPI.Common.Utils
{
    static class Helper
    {
        public static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static void EatException(Action action)
        {
            Ensure.NotNull(action, "action");
            try
            {
                action();
            }
// ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
            {
            }
        }

        public static T EatException<T>(Func<T> func, T defaultValue = default(T))
        {
            Ensure.NotNull(func, "func");
            try
            {
                return func();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }
   
        public static string FormatBinaryDump(byte[] logBulk)
        {
            return FormatBinaryDump(new ArraySegment<byte>(logBulk ?? Empty.ByteArray));
        }

        public static string FormatBinaryDump(ArraySegment<byte> logBulk)
        {
            if (logBulk.Count == 0)
                return "--- NO DATA ---";

            var sb = new StringBuilder();
            int cur = 0;
            int len = logBulk.Count;
            for (int row = 0, rows = (logBulk.Count + 15) / 16; row < rows; ++row)
            {
                sb.AppendFormat("{0:000000}:", row * 16);
                for (int i = 0; i < 16; ++i, ++cur)
                {
                    if (cur >= len)
                        sb.Append("   ");
                    else
                        sb.AppendFormat(" {0:X2}", logBulk.Array[logBulk.Offset + cur]);
                }
                sb.Append("  | ");
                cur -= 16;
                for (int i = 0; i < 16; ++i, ++cur)
                {
                    if (cur < len)
                    {
                        var b = (char)logBulk.Array[logBulk.Offset + cur];
                        sb.Append(char.IsControl(b) ? '.' : b);
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}