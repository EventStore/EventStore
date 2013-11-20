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

namespace EventStore.ClientAPI.Common.Log
{
    class DebugLogger : ILogger
    {
        public void Error(string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Log("ERROR", format, args));
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Log("ERROR", ex, format, args));
        }

        public void Debug(string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Log("DEBUG", format, args));
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Log("DEBUG", ex, format, args));
        }

        public void Info(string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Log("INFO", format, args));
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Log("INFO", ex, format, args));
        }


        private string Log(string level, string format, params object[] args)
        {
            return string.Format("{0}: {1}", level, args.Length == 0 ? format : string.Format(format, args));
        }

        private string Log(string level, Exception exc, string format, params object[] args)
        {
            return string.Format("{0} EXCEPTION: {1}\nException: {2}", level, args.Length == 0 ? format : string.Format(format, args), exc);
        }
    }
}