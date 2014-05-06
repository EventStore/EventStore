/*
GFY NOT USED
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
        public string Usage { get { return "WRH [<stream-id> <expected-version> <data> <metadata> <only-if-master>]"; } }
        public string Keyword { get { return "WRH"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            var expectedVersion = ExpectedVersion.Any;
            string metadata = "{'user' : 'test'}";
            var requireMaster = false;
            var data = "{'a' : 3 }";
            if (args.Length > 0)
            {
                if (args.Length != 5)
                    return false;
                eventStreamId = args[0];
                expectedVersion = args[1].ToUpper() == "ANY" ? ExpectedVersion.Any : int.Parse(args[1]);
                data = args[2];
                metadata = args[3];
                requireMaster = bool.Parse(args[4]);
            }

            context.IsAsync();

            var client = new HttpAsyncClient();
            var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}", eventStreamId);
            context.Log.Info("Writing to {0}...", url);
            var msg = "[{'eventType': 'fooevent', 'eventId' : '" + Guid.NewGuid() + "'" + ",'data' : " + data + ", 'metadata' : " + metadata + "}]";

            var sw = Stopwatch.StartNew();
            client.Post(
                url,
                msg,
                Codec.Json.ContentType,
                new Dictionary<string, string>
                {
                        {SystemHeaders.ExpectedVersion, expectedVersion.ToString()},
                        {SystemHeaders.RequireMaster, requireMaster ? "True" : "False"}
                },
                TimeSpan.FromMilliseconds(10000),
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
*/