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
using EventStore.Common.Utils;

namespace EventStore.Common.Log
{
    class LazyLogger : ILogger
    {
        private readonly Lazy<ILogger> _logger;

        public LazyLogger(Func<ILogger> factory)
        {
            Ensure.NotNull(factory, "factory");
            _logger = new Lazy<ILogger>(factory);
        }

        public void Fatal(string text)
        {
            _logger.Value.Fatal(text);
        }

        public void Error(string text)
        {
            _logger.Value.Error(text);
        }

        public void Info(string text)
        {
            _logger.Value.Info(text);
        }

        public void Debug(string text)
        {
            _logger.Value.Debug(text);
        }

        public void Trace(string text)
        {
            _logger.Value.Trace(text);
        }

        public void Fatal(string format, params object[] args)
        {
            _logger.Value.Fatal(format, args);
        }

        public void Error(string format, params object[] args)
        {
            _logger.Value.Error(format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.Value.Info(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.Value.Debug(format, args);
        }

        public void Trace(string format, params object[] args)
        {
            _logger.Value.Trace(format, args);
        }

        public void FatalException(Exception exc, string format)
        {
            _logger.Value.FatalException(exc, format);
        }

        public void ErrorException(Exception exc, string format)
        {
            _logger.Value.ErrorException(exc, format);
        }

        public void InfoException(Exception exc, string format)
        {
            _logger.Value.InfoException(exc, format);
        }

        public void DebugException(Exception exc, string format)
        {
            _logger.Value.DebugException(exc, format);
        }

        public void TraceException(Exception exc, string format)
        {
            _logger.Value.TraceException(exc, format);
        }

        public void FatalException(Exception exc, string format, params object[] args)
        {
            _logger.Value.FatalException(exc, format, args);
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            _logger.Value.ErrorException(exc, format, args);
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
            _logger.Value.InfoException(exc, format, args);
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            _logger.Value.DebugException(exc, format, args);
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            _logger.Value.TraceException(exc, format, args);
        }
    }
}
