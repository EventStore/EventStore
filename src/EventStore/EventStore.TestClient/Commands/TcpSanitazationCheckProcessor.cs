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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Services.Transport.Tcp;
using System.Linq;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    public class TcpSanitazationCheckProcessor : ICmdProcessor
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpSanitazationCheckProcessor>();

        public string Keyword
        {
            get
            {
                return "CHKTCP";
            }
        }

        public string Usage
        {
            get
            {
                return Keyword;
            }
        }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            context.IsAsync();

            var commandsToCkeck = new[]
                                      {
                                          (byte) TcpCommand.WriteEvents,
                                          (byte) TcpCommand.TransactionStart,
                                          (byte) TcpCommand.TransactionWrite,
                                          (byte) TcpCommand.TransactionCommit,
                                          (byte) TcpCommand.DeleteStream,
                                          (byte) TcpCommand.ReadEvent,
                                          (byte) TcpCommand.ReadStreamEventsForward,
                                          (byte) TcpCommand.ReadStreamEventsBackward,
                                          (byte) TcpCommand.ReadAllEventsForward,
                                          (byte) TcpCommand.ReadAllEventsBackward,
                                          (byte) TcpCommand.SubscribeToStream,
                                          (byte) TcpCommand.UnsubscribeFromStream,
                                      };

            var packages = commandsToCkeck.Select(c => new TcpPackage((TcpCommand)c, Guid.NewGuid(), new byte[] { 0, 1, 0, 1 }).AsByteArray())
                                          .Union(new[]
                                                     {
                                                         BitConverter.GetBytes(int.MaxValue).Union(new byte[] {1, 2, 3, 4}).ToArray(),
                                                         BitConverter.GetBytes(int.MinValue).Union(new byte[] {1, 2, 3, 4}).ToArray(),
                                                         BitConverter.GetBytes(0).Union(Enumerable.Range(0, 256).Select(x => (byte) x)).ToArray()
                                                     });

            int step = 0;
            foreach (var pkg in packages)
            {
                var established = new AutoResetEvent(false);
                var dropped = new AutoResetEvent(false);

                if (step < commandsToCkeck.Length)
                    Console.WriteLine("{0} Starting step {1} ({2}) {0}", new string('#', 20), step, (TcpCommand) commandsToCkeck[step]);
                else
                    Console.WriteLine("{0} Starting step {1} (RANDOM BYTES) {0}", new string('#', 20), step);

                var connection = context.Client.CreateTcpConnection(
                    context,
                    (conn, package) =>
                    {
                        if (package.Command != TcpCommand.BadRequest)
                            context.Fail(null, string.Format("Bad request expected, got {0}!", package.Command));
                    },
                    conn => established.Set(),
                    (conn, err) => dropped.Set());

                established.WaitOne();
                connection.EnqueueSend(pkg);
                dropped.WaitOne();

                if (step < commandsToCkeck.Length)
                    Console.WriteLine("{0} Step {1} ({2}) Completed {0}", new string('#', 20), step, (TcpCommand)commandsToCkeck[step]);
                else
                    Console.WriteLine("{0} Step {1} (RANDOM BYTES) Completed {0}", new string('#', 20), step);

                step++;
            }

            Log.Info("Sent {0} packages. {1} invalid dtos, {2} bar formatted packages. Got {3} BadRequests. Success",
                     packages.Count(),
                     commandsToCkeck.Length,
                     packages.Count() - commandsToCkeck.Length,
                     packages.Count());

            Log.Info("Now sending raw bytes...");
            try
            {
                SendRaw(context.Client.TcpEndpoint, BitConverter.GetBytes(int.MaxValue));
                SendRaw(context.Client.TcpEndpoint, BitConverter.GetBytes(int.MinValue));

                SendRaw(context.Client.TcpEndpoint, BitConverter.GetBytes(double.MinValue));
                SendRaw(context.Client.TcpEndpoint, BitConverter.GetBytes(double.MinValue));

                SendRaw(context.Client.TcpEndpoint, BitConverter.GetBytes(new Random().NextDouble()));
            }
            catch (Exception e)
            {
                context.Fail(e, "Raw bytes sent failed");
                return false;
            }

            context.Success();
            return true;
        }

        private void SendRaw(IPEndPoint endPoint, byte[] package)
        {
            using (var client = new TcpClient())
            {
                client.Connect(endPoint);
                using (var stream = client.GetStream())
                {
                    stream.Write(package, 0, package.Length);
                }
            }
        }
    }
}
