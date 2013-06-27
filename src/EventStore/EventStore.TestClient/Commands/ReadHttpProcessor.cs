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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Services;
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
            var eventNumber = 0;
            var resolveLinkTos = false;
            var requireMaster = false;

            if (args.Length > 0)
            {
                if (args.Length > 3)
                    return false;
                eventStreamId = args[0];
                if (args.Length >= 2)
                    eventNumber = int.Parse(args[1]);
                if (args.Length >= 3)
                    requireMaster = bool.Parse(args[2]);
            }

            context.IsAsync();

            var client = new HttpAsyncClient();
            var readUrl = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}/{1}?format=json", eventStreamId,
                                                                eventNumber == -1 ? "head" : eventNumber.ToString());

            context.Log.Info("[{0}]: Reading...", context.Client.HttpEndpoint);

            var sw = Stopwatch.StartNew();
            client.Get(readUrl,
                       new Dictionary<string, string>
                       {
                            {SystemHeaders.ResolveLinkTos, resolveLinkTos ? "True" : "False"},
                            {SystemHeaders.RequireMaster, requireMaster ? "True" : "False"}
                       }, 
                       response =>
                       {
                           sw.Stop();
                           context.Log.Info("[{0} ({1})]: READ events from <{2}>.",
                                            response.HttpStatusCode, response.StatusDescription, eventStreamId);
                           context.Log.Info("Response:\n{0}\n\n{1}\n",
                                            string.Join("\n", response.Headers.AllKeys.Select(x => string.Format("{0,-20}: {1}", x, response.Headers[x]))),
                                            response.Body);
                           context.Log.Info("Read request took: {0}.", sw.Elapsed);
                           context.Success();
                       },
                       e => context.Fail(e, string.Format("READ events from <{0}>: FAILED", eventStreamId)));

            return true;
        }
    }
}