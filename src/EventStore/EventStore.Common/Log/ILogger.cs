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

namespace EventStore.Common.Log
{
    public interface ILogger
    {
        void Flush(TimeSpan? maxTimeToWait = null);

        void Fatal(string text);
        void Error(string text);
        void Info(string text);
        void Debug(string text);
        void Trace(string text);

        void Fatal(string format, params object[] args);
        void Error(string format, params object[] args);
        void Info(string format, params object[] args);
        void Debug(string format, params object[] args);
        void Trace(string format, params object[] args);

        void FatalException(Exception exc, string text);
        void ErrorException(Exception exc, string text);
        void InfoException(Exception exc, string text);
        void DebugException(Exception exc, string text);
        void TraceException(Exception exc, string text);

        void FatalException(Exception exc, string format, params object[] args);
        void ErrorException(Exception exc, string format, params object[] args);
        void InfoException(Exception exc, string format, params object[] args);
        void DebugException(Exception exc, string format, params object[] args);
        void TraceException(Exception exc, string format, params object[] args);
    }
}