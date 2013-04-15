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
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Client;

namespace EventStore.TestClient.Commands
{
    internal class ReadHttpProcessor : ICmdProcessor
    {
        public string Usage { get { return "RDH [<stream-id> [<from-version>]]"; } }
        public string Keyword { get { return "RDH"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            var version = 0;

            if (args.Length > 0)
            {
                if (args.Length > 2)
                    return false;
                eventStreamId = args[0];
                if (args.Length == 2)
                    version = int.Parse(args[1]);
            }

            context.IsAsync();

            var client = new HttpAsyncClient();
            var readUrl = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}/{1}?format=json", eventStreamId, version);

            context.Log.Info("[{0}]: Reading...", context.Client.HttpEndpoint);

            var sw = Stopwatch.StartNew();
            client.Get(readUrl,
                       response =>
                       {
                           sw.Stop();
                           context.Log.Info("READ events from <{0}>: \n{1}", eventStreamId, response.Body);
                           context.Log.Info("Read request took: {0}.", sw.Elapsed);
                           context.Success();
                       },
                       e => context.Fail(e, string.Format("READ events from <{0}>: FAILED", eventStreamId)));

            return true;
        }
    }
}