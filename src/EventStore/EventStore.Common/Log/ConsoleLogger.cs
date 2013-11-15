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
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace EventStore.Common.Log
{
    public class ConsoleLogger : ILogger
    {
        public ConsoleLogger(string name = "")
        {
        }

        public void Flush(TimeSpan? maxTimeToWait = null)
        {
        }

        public void Fatal(string text)
        {
            Console.WriteLine(Log("FATAL", text, Utils.Empty.ObjectArray));
        }

        public void Error(string text)
        {
            Console.WriteLine(Log("ERROR", text, Utils.Empty.ObjectArray));
        }

        public void Info(string text)
        {
            Console.WriteLine(Log("INFO ", text, Utils.Empty.ObjectArray));
        }

        public void Debug(string text)
        {
            Console.WriteLine(Log("DEBUG", text, Utils.Empty.ObjectArray));
        }

        public void Trace(string text)
        {
            Console.WriteLine(Log("TRACE", text, Utils.Empty.ObjectArray));
        }


        public void Fatal(string format, params object[] args)
        {
            Console.WriteLine(Log("FATAL", format, args));
        }

        public void Error(string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", format, args));
        }

        public void Info(string format, params object[] args)
        {
            Console.WriteLine(Log("INFO ", format, args));
        }

        public void Debug(string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", format, args));
        }

        public void Trace(string format, params object[] args)
        {
            Console.WriteLine(Log("TRACE", format, args));
        }


        public void FatalException(Exception exc, string format)
        {
            Console.WriteLine(Log("FATAL", exc, format, Utils.Empty.ObjectArray));
        }

        public void ErrorException(Exception exc, string format)
        {
            Console.WriteLine(Log("ERROR", exc, format, Utils.Empty.ObjectArray));
        }

        public void InfoException(Exception exc, string format)
        {
            Console.WriteLine(Log("INFO ", exc, format, Utils.Empty.ObjectArray));
        }

        public void DebugException(Exception exc, string format)
        {
            Console.WriteLine(Log("DEBUG", exc, format, Utils.Empty.ObjectArray));
        }

        public void TraceException(Exception exc, string format)
        {
            Console.WriteLine(Log("TRACE", exc, format, Utils.Empty.ObjectArray));
        }


        public void FatalException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("FATAL", exc, format, args));
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", exc, format, args));
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("INFO ", exc, format, args));
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", exc, format, args));
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("TRACE", exc, format, args));
        }

        private static readonly int ProcessId = Process.GetCurrentProcess().Id;

        private string Log(string level, string format, params object[] args)
        {
            return string.Format("[{0:00000},{1:00},{2:HH:mm:ss.fff},{3}] {4}",
                                 ProcessId,
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 args.Length == 0 ? format : string.Format(format, args));
        }

        private string Log(string level, Exception exc, string format, params object[] args)
        {
            var sb = new StringBuilder();
            while (exc != null)
            {
                sb.AppendLine();
                sb.AppendLine(exc.ToString());
                exc = exc.InnerException;
            }

            return string.Format("[{0:00000},{1:00},{2:HH:mm:ss.fff},{3}] {4}\nEXCEPTION(S) OCCURRED:{5}",
                                 ProcessId,
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 args.Length == 0 ? format : string.Format(format, args),
                                 sb);
        }
    }
}
