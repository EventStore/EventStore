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
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class ReadAllProcessor : ICmdProcessor
    {
        public string Usage { get { return "RDALL [[F|B] [<commit pos> <prepare pos> [<only-if-master>]]]"; } }
        public string Keyword { get { return "RDALL"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            bool forward = true;
            long commitPos = 0;
            long preparePos = 0;
            bool posOverriden = false;
            bool resolveLinkTos = false;
            bool requireMaster = false;

            if (args.Length > 0)
            {
                if (args.Length != 1 && args.Length != 3 && args.Length != 4)
                    return false;

                if (args[0].ToUpper() == "F")
                    forward = true;
                else if (args[0].ToUpper() == "B")
                    forward = false;
                else
                    return false;

                if (args.Length >= 3)
                {
                    posOverriden = true;
                    if (!long.TryParse(args[1], out commitPos) || !long.TryParse(args[2], out preparePos))
                        return false;
                }
                if (args.Length >= 4)
                    requireMaster = bool.Parse(args[3]);

            }

            if (!posOverriden)
            {
                commitPos = forward ? 0 : -1;
                preparePos = forward ? 0 : -1;
            }

            context.IsAsync();

            int total = 0;
            var sw = new Stopwatch();
            var tcpCommand = forward ? TcpCommand.ReadAllEventsForward : TcpCommand.ReadAllEventsBackward;

            context.Client.CreateTcpConnection(
                context,
                connectionEstablished: conn =>
                {
                    context.Log.Info("[{0}, L{1}]: Reading all {2}...", conn.RemoteEndPoint, conn.LocalEndPoint, forward ? "FORWARD" : "BACKWARD");

                    var readDto = new TcpClientMessageDto.ReadAllEvents(commitPos, preparePos, 10, resolveLinkTos, requireMaster);
                    var package = new TcpPackage(tcpCommand, Guid.NewGuid(), readDto.Serialize()).AsByteArray();
                    sw.Start();
                    conn.EnqueueSend(package);
                },
                handlePackage: (conn, pkg) =>
                {
                    if (pkg.Command != tcpCommand)
                    {
                        context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                        return;
                    }

                    var dto = pkg.Data.Deserialize<TcpClientMessageDto.ReadAllEventsCompleted>();
                    if (dto.Events.IsEmpty())
                    {
                        sw.Stop();
                        context.Log.Info("=== Reading ALL {2} completed in {0}. Total read: {1}", sw.Elapsed, total, forward ? "FORWARD" : "BACKWARD");
                        context.Success();
                        conn.Close();
                        return;
                    }
                    var sb = new StringBuilder();
                    for (int i = 0; i < dto.Events.Length; ++i)
                    {
                        var evnt = dto.Events[i].Event;
                        sb.AppendFormat("\n{0}:\tStreamId: {1},\n\tEventNumber: {2},\n\tData:\n{3},\n\tEventType: {4}\n",
                                        total,
                                        evnt.EventStreamId,
                                        evnt.EventNumber,
                                        Helper.UTF8NoBom.GetString(evnt.Data),
                                        evnt.EventType);
                        total += 1;
                    }
                    context.Log.Info("Next {0} events read:\n{1}", dto.Events.Length, sb.ToString());

                    var readDto = new TcpClientMessageDto.ReadAllEvents(dto.NextCommitPosition, dto.NextPreparePosition, 10, resolveLinkTos, requireMaster);
                    var package = new TcpPackage(tcpCommand, Guid.NewGuid(), readDto.Serialize()).AsByteArray();
                    conn.EnqueueSend(package);
                },
                connectionClosed: (connection, error) => context.Fail(reason: "Connection was closed prematurely."));

            context.WaitForCompletion();
            return true;
        }
    }
}
