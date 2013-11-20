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

using System;
using System.Text;
using System.Threading;

namespace EventStore.ClientAPI.Common.Log
{
    /// <summary>
    /// Implementation of <see cref="ILogger"/> which outputs to <see cref="Console"/>.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        /// <summary>
        /// Writes an error to the logger
        /// </summary>
        /// <param name="format">Format string for the log message.</param>
        /// <param name="args">Arguments to be inserted into the format string.</param>
        public void Error(string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", format, args));
        }

        /// <summary>
        /// Writes an error to the logger
        /// </summary>
        /// <param name="ex">A thrown exception.</param>
        /// <param name="format">Format string for the log message.</param>
        /// <param name="args">Arguments to be inserted into the format string.</param>
        public void Error(Exception ex, string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", ex, format, args));
        }

        /// <summary>
        /// Writes a debug message to the logger
        /// </summary>
        /// <param name="format">Format string for the log message.</param>
        /// <param name="args">Arguments to be inserted into the format string.</param>
        public void Debug(string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", format, args));
        }

        /// <summary>
        /// Writes a debug message to the logger
        /// </summary>
        /// <param name="ex">A thrown exception.</param>
        /// <param name="format">Format string for the log message.</param>
        /// <param name="args">Arguments to be inserted into the format string.</param>
        public void Debug(Exception ex, string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", ex, format, args));
        }

        /// <summary>
        /// Writes an information message to the logger
        /// </summary>
        /// <param name="format">Format string for the log message.</param>
        /// <param name="args">Arguments to be inserted into the format string.</param>
        public void Info(string format, params object[] args)
        {
            Console.WriteLine(Log("INFO", format, args));
        }

        /// <summary>
        /// Writes an information message to the logger
        /// </summary>
        /// <param name="ex">A thrown exception.</param>
        /// <param name="format">Format string for the log message.</param>
        /// <param name="args">Arguments to be inserted into the format string.</param>
        public void Info(Exception ex, string format, params object[] args)
        {
            Console.WriteLine(Log("INFO", ex, format, args));
        }

        private string Log(string level, string format, params object[] args)
        {
            return string.Format("[{0:00},{1:HH:mm:ss.fff},{2}] {3}",
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

            return string.Format("[{0:00},{1:HH:mm:ss.fff},{2}] {3}\nEXCEPTION(S) OCCURRED:{4}",
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 args.Length == 0 ? format : string.Format(format, args),
                                 sb);
        }
    }
}