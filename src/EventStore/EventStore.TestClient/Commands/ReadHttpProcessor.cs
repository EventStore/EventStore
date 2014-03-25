using System;
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
                       TimeSpan.FromMilliseconds(10000),
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