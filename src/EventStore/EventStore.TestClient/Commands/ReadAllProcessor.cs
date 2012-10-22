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
using System.Net.Sockets;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class ReadAllProcessor : ICmdProcessor
    {
        public string Usage { get { return "RDALL [[F|B] [<commit pos> <prepare pos>]]"; } }
        public string Keyword { get { return "RDALL"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            bool forward = true;
            long commitPos = 0;
            long preparePos = 0;
            bool posOverriden = false;

            if (args.Length > 0)
            {
                if (args.Length != 1 && args.Length != 3)
                    return false;

                if (args[0].ToUpper() == "F")
                    forward = true;
                else if (args[0].ToUpper() == "B")
                    forward = false;
                else
                    return false;

                if (args.Length == 3)
                {
                    posOverriden = true;
                    if (!long.TryParse(args[1], out commitPos) || !long.TryParse(args[2], out preparePos))
                        return false;
                }
            }

            if (!posOverriden)
            {
                commitPos = forward ? 0 : -1;
                preparePos = forward ? 0 : -1;
            }

            context.IsAsync();

            int total = 0;
            var sw = new Stopwatch();

            context.Client.CreateTcpConnection(
                context,
                connectionEstablished: conn =>
                {
                    context.Log.Info("[{0}]: Reading all {0}...", conn.EffectiveEndPoint, forward ? "FORWARD" : "BACKWARD");
                    sw.Start();

                    var readDto = forward
                                    ? (object)new ClientMessageDto.ReadAllEventsForward(commitPos, preparePos, 10, false)
                                    : new ClientMessageDto.ReadAllEventsBackward(commitPos, preparePos, 10, false);
                    var package = new TcpPackage(forward ? TcpCommand.ReadAllEventsForward : TcpCommand.ReadAllEventsBackward,
                                                 Guid.NewGuid(),
                                                 readDto.Serialize());
                    conn.EnqueueSend(package.AsByteArray());
                },
                handlePackage: (conn, pkg) =>
                {
                    EventLinkPair[] records;
                    long nextCommitPos;
                    long nextPreparePos;

                    if (forward)
                    {
                        if (pkg.Command != TcpCommand.ReadAllEventsForwardCompleted)
                        {
                            context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                            return;
                        }

                        var dto = pkg.Data.Deserialize<ClientMessageDto.ReadAllEventsForwardCompleted>();
                        records = dto.Events;
                        nextCommitPos = dto.NextCommitPosition;
                        nextPreparePos = dto.NextPreparePosition;
                    }
                    else
                    {
                        if (pkg.Command != TcpCommand.ReadAllEventsBackwardCompleted)
                        {
                            context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                            return;
                        }

                        var dto = pkg.Data.Deserialize<ClientMessageDto.ReadAllEventsBackwardCompleted>();
                        records = dto.Events;
                        nextCommitPos = dto.NextCommitPosition;
                        nextPreparePos = dto.NextPreparePosition;
                    }

                    if (records == null || records.Length == 0)
                    {
                        sw.Stop();
                        context.Log.Info("=== Reading ALL {2} completed in {0}. Total read: {1}",
                                         sw.Elapsed,
                                         total,
                                         forward ? "FORWARD" : "BACKWARD");
                        conn.Close();
                        context.Success();
                        return;
                    }
                    var sb = new StringBuilder();
                    for (int i = 0; i < records.Length; ++i)
                    {
                        var evnt = records[i].Event;
                        sb.AppendFormat("\n{0}:\tLogPosition: {1},\n\tStreamId: {2},\n\tEventNumber: {3},\n\tData:\n{4},\n\tEventType: {5}\n",
                                        total,
                                        evnt.LogPosition,
                                        evnt.EventStreamId,
                                        evnt.EventNumber,
                                        Encoding.UTF8.GetString(evnt.Data),
                                        evnt.EventType);
                        total += 1;
                    }
                    context.Log.Info("Next {0} events read:\n{1}", records.Length, sb.ToString());

                    var readDto = forward
                                    ? (object)new ClientMessageDto.ReadAllEventsForward(nextCommitPos, nextPreparePos, 10, false)
                                    : new ClientMessageDto.ReadAllEventsBackward(nextCommitPos, nextPreparePos, 10, false);
                    var package = new TcpPackage(forward ? TcpCommand.ReadAllEventsForward : TcpCommand.ReadAllEventsBackward,
                                                 Guid.NewGuid(),
                                                 readDto.Serialize());
                    conn.EnqueueSend(package.AsByteArray());


                },
                connectionClosed: (connection, error) =>
                {
                    if (error == SocketError.Success)
                        context.Success();
                    else
                        context.Fail();
                });

            context.WaitForCompletion();
            return true;
        }
    }
}
