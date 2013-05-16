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
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Helpers
{
    public class ClientApiLoggerBridge : EventStore.ClientAPI.ILogger
    {
        public static readonly ClientApiLoggerBridge Default = new ClientApiLoggerBridge(LogManager.GetLogger("client-api"));

        private readonly ILogger _log;

        public ClientApiLoggerBridge(ILogger log)
        {
            Ensure.NotNull(log, "log");
            _log = log;
        }

        public void Error(string format, params object[] args)
        {
            if (args.Length == 0)
                _log.Error(format);
            else
                _log.Error(format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            if (args.Length == 0)
                _log.ErrorException(ex, format);
            else
                _log.ErrorException(ex, format, args);
        }

        public void Info(string format, params object[] args)
        {
            if (args.Length == 0)
                _log.Info(format);
            else
                _log.Info(format, args);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            if (args.Length == 0)
                _log.InfoException(ex, format);
            else
                _log.InfoException(ex, format, args);
        }

        public void Debug(string format, params object[] args)
        {
            if (args.Length == 0)
                _log.Debug(format);
            else
                _log.Debug(format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            if (args.Length == 0)
                _log.DebugException(ex, format);
            else
                _log.DebugException(ex, format, args);
        }
    }
}
