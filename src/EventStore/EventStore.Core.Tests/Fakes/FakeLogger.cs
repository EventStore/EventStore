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
using EventStore.Common.Log;

namespace EventStore.Core.Tests.Fakes
{
    public class FakeLogger: ILogger 
    {
        public void Flush(TimeSpan? maxTimeToWait = null)
        {
        }

        public void Fatal(string text)
        {
        }

        public void Error(string text)
        {
        }

        public void Info(string text)
        {
        }

        public void Debug(string text)
        {
        }

        public void Trace(string text)
        {
        }

        public void Fatal(string format, params object[] args)
        {
        }

        public void Error(string format, params object[] args)
        {
        }

        public void Info(string format, params object[] args)
        {
        }

        public void Debug(string format, params object[] args)
        {
        }

        public void Trace(string format, params object[] args)
        {
        }

        public void FatalException(Exception exc, string text)
        {
        }

        public void ErrorException(Exception exc, string text)
        {
        }

        public void InfoException(Exception exc, string text)
        {
        }

        public void DebugException(Exception exc, string text)
        {
        }

        public void TraceException(Exception exc, string text)
        {
        }

        public void FatalException(Exception exc, string format, params object[] args)
        {
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
        }
    }
}
