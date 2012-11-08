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
using System.Linq;
using System.Threading;
using NLog;

namespace EventStore.Common.Log
{
    public class NLogger : ILogger
    {
        private readonly Logger _logger;

        public NLogger(string name)
        {
            _logger = NLog.LogManager.GetLogger(name);
        }

        public void Flush(TimeSpan? maxTimeToWait = null)
        {
            var asyncs = NLog.LogManager.Configuration.AllTargets.OfType<NLog.Targets.Wrappers.AsyncTargetWrapper>().ToArray();
            var countdown = new CountdownEvent(asyncs.Length);
            foreach (var wrapper in asyncs)
            {
                wrapper.Flush(x => countdown.Signal());
            }
            countdown.Wait(maxTimeToWait ?? TimeSpan.FromMilliseconds(500));
        }

        public void Fatal(string text)
        {
            _logger.Fatal(text);
        }

        public void Error(string text)
        {
            _logger.Error(text);
        }

        public void Info(string text)
        {
            _logger.Info(text);
        }

        public void Debug(string text)
        {
            _logger.Debug(text);
        }

        public void Trace(string text)
        {
            _logger.Trace(text);
        }


        public void Fatal(string format, params object[] args)
        {
            _logger.Fatal(format, args);
        }

        public void Error(string format, params object[] args)
        {
            _logger.Error(format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.Info(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.Debug(format, args);
        }

        public void Trace(string format, params object[] args)
        {
            _logger.Trace(format, args);
        }


        public void FatalException(Exception exc, string format)
        {
            _logger.FatalException(format, exc);
        }

        public void ErrorException(Exception exc, string format)
        {
            _logger.ErrorException(format, exc);
        }

        public void InfoException(Exception exc, string format)
        {
            _logger.InfoException(format, exc);
        }

        public void DebugException(Exception exc, string format)
        {
            _logger.DebugException(format, exc);
        }

        public void TraceException(Exception exc, string format)
        {
            _logger.TraceException(format, exc);
        }


        public void FatalException(Exception exc, string format, params object[] args)
        {
            _logger.FatalException(string.Format(format, args), exc);
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            _logger.ErrorException(string.Format(format, args), exc);
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
            _logger.InfoException(string.Format(format, args), exc);
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            _logger.DebugException(string.Format(format, args), exc);
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            _logger.TraceException(string.Format(format, args), exc);
        }
    }
}
