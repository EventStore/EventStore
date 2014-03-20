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
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using EventStore.Common.Log;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Common.Utils
{
    public static class FileStreamExtensions
    {
        private static readonly ILogger Log = LogManager.GetLogger("FileStreamExtensions");
        private static readonly Action<FileStream> FlushSafe;
        private static readonly Func<FileStream, SafeFileHandle> GetFileHandle;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushFileBuffers(SafeFileHandle hFile);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //static extern bool FlushViewOfFile(IntPtr lpBaseAddress, UIntPtr dwNumberOfBytesToFlush);

        static FileStreamExtensions()
        {

#if NOFLUSH
                FlushSafe = f => f.Flush(flushToDisk: false);
            return;

#endif
            if (Runtime.IsMono)
                FlushSafe = f => f.Flush(flushToDisk: true);
            else
            {
                try
                {
                    ParameterExpression arg = Expression.Parameter(typeof(FileStream), "f");
                    Expression expr = Expression.Field(arg, typeof(FileStream).GetField("_handle", BindingFlags.Instance | BindingFlags.NonPublic));
                    GetFileHandle = Expression.Lambda<Func<FileStream, SafeFileHandle>>(expr, arg).Compile();
                    FlushSafe = f =>
                    {
                        f.Flush(flushToDisk: false);
                        if (!FlushFileBuffers(GetFileHandle(f)))
                            throw new Exception(string.Format("FlushFileBuffers failed with err: {0}", Marshal.GetLastWin32Error()));
                    };
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while compiling sneaky SafeFileHandle getter.");
                    FlushSafe = f => f.Flush(flushToDisk: true);
                }
            }
        }

        public static void FlushToDisk(this FileStream fs)
        {
            FlushSafe(fs);
        }
    }
}
