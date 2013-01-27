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
using System.Text;

namespace EventStore.Projections.Core.Services.v8
{
    public class DefaultV8ProjectionStateHandler : V8ProjectionStateHandler
    {
        private static readonly string _jsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prelude");

        public DefaultV8ProjectionStateHandler(
            string query, Action<string> logger, Action<int, Action> cancelCallbackFactory)
            : base("1Prelude", query, GetModuleSource, logger, cancelCallbackFactory)
        {
        }

        public static Tuple<string, string> GetModuleSource(string name)
        {
            var fullScriptFileName = Path.GetFullPath(Path.Combine(_jsPath, name + ".js"));
            var scriptSource = File.ReadAllText(fullScriptFileName, Encoding.UTF8);
            return Tuple.Create(scriptSource, fullScriptFileName);
        }
    }
}
