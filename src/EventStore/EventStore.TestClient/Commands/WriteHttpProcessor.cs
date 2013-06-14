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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;

namespace EventStore.TestClient.Commands
{
    internal class WriteHttpProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRH [<stream-id> <expected-version> <data> <metadata> <allow-forwarding>]"; } }
        public string Keyword { get { return "WRH"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            var expectedVersion = ExpectedVersion.Any;
            var data = "test-data";
            string metadata = null;
            var allowForwarding = true;

            if (args.Length > 0)
            {
                if (args.Length != 5)
                    return false;
                eventStreamId = args[0];
                expectedVersion = args[1].ToUpper() == "ANY" ? ExpectedVersion.Any : int.Parse(args[1]);
                data = args[2];
                metadata = args[3];
                allowForwarding = bool.Parse(args[4]);
            }

            context.IsAsync();

            var client = new HttpAsyncClient();
            var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}", eventStreamId);
            context.Log.Info("Writing to {0}...", url);

            var request = Codec.Xml.To(new[] { new HttpClientMessageDto.ClientEventText(Guid.NewGuid(), "type", data, metadata) });

            var sw = Stopwatch.StartNew();
            client.Post(
                url,
                request,
                Codec.Xml.ContentType,
                new Dictionary<string, string>
                {
                        {SystemHeader.ExpectedVersion, expectedVersion.ToString()},
                        {SystemHeader.Forwarding, allowForwarding ? "Enable" : "Disable"}
                }, 
                response =>
                {
                    sw.Stop();
                    if (response.HttpStatusCode == HttpStatusCode.Created)
                        context.Log.Info("Successfully written");
                    else
                    {
                        context.Log.Info("Error while writing: [{0}] - [{1}]", response.HttpStatusCode, response.StatusDescription);
                        context.Log.Info("Response:\n{0}\n\n{1}\n",
                                         string.Join("\n", response.Headers.AllKeys.Select(x => string.Format("{0,-20}: {1}", x, response.Headers[x]))),
                                         response.Body);
                    }

                    context.Log.Info("Write request took: {0}.", sw.Elapsed);
                    context.Success();
                },
                e =>
                {
                    context.Log.ErrorException(e, "Error during POST");
                    context.Fail(e, "Error during POST.");
                });

            return true;
        }
    }
}