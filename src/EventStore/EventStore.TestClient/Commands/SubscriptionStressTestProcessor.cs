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
using System.Diagnostics;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Tests.Helper;

namespace EventStore.TestClient.Commands
{
    internal class SubscriptionStressTestProcessor : ICmdProcessor
    {
        public string Usage { get { return "SST [<subscription-count>]"; } }
        public string Keyword { get { return "SST"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int subscriptionCount = 5000;

            if (args.Length > 0)
            {
                if (args.Length > 1)
                    return false;
                subscriptionCount = int.Parse(args[0]);
            }

            context.IsAsync();

            var conn = EventStoreConnection.Create(ConnectionSettings.Create()
                                                                     .UseCustomLogger(new ClientApiLoggerBridge(context.Log))
                                                                     .FailOnNoServerResponse()
                                                                     /*.EnableVerboseLogging()*/);
            conn.Connect(context.Client.TcpEndpoint);

            long appearedCnt = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < subscriptionCount; ++i)
            {
                var subscr = conn.SubscribeToStream(
                    string.Format("stream-{0}", i),
                    false,
                    (s, e) =>
                    {
                        var c = Interlocked.Increment(ref appearedCnt);
                        if (c%1000 == 0) Console.Write('\'');
                        if (c%100000 == 0)
                        {
                            context.Log.Trace("Received total {0} events ({1} per sec)...", c, 100000.0/sw.Elapsed.TotalSeconds);
                            sw.Restart();
                        }
                    }).Result;
            }
            context.Log.Info("Subscribed to {0} streams...", subscriptionCount);
            return true;
        }
    }
}
