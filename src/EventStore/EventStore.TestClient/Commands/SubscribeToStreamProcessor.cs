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
using System.Net.Sockets;
using System.Text;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class SubscribeToStreamProcessor : ICmdProcessor
    {
        public string Usage { get { return "SUBSCR [<stream_1> <stream_2> ... <stream_n>]"; } }
        public string Keyword { get { return "SUBSCR"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            context.IsAsync();

            var connection = context.Client.CreateTcpConnection(
                    context,
                    connectionEstablished: conn =>
                    {
                    },
                    handlePackage: (conn, pkg) =>
                    {
                        switch (pkg.Command)
                        {
                            case TcpCommand.StreamEventAppeared:
                            {
                                var dto = pkg.Data.Deserialize<ClientMessageDto.StreamEventAppeared>();
                                context.Log.Info("NEW EVENT:\n\n"
                                                 + "\tEventStreamId: {0}\n"
                                                 + "\tEventNumber:   {1}\n"
                                                 + "\tEventType:     {2}\n"
                                                 + "\tData:          {3}\n"
                                                 + "\tMetadata:      {4}\n",
                                                 dto.EventStreamId,
                                                 dto.EventNumber,
                                                 dto.EventType,
                                                 Encoding.UTF8.GetString(dto.Data ?? new byte[0]),
                                                 Encoding.UTF8.GetString(dto.Metadata ?? new byte[0]));
                                break;
                            }
                            case TcpCommand.SubscriptionDropped:
                            {
                                var dto = pkg.Data.Deserialize<ClientMessageDto.SubscriptionDropped>();
                                context.Log.Error("Subscription to <{0}> WAS DROPPED!", dto.EventStreamId);
                                break;
                            }
                            case TcpCommand.SubscriptionToAllDropped:
                            {
                                var dto = pkg.Data.Deserialize<ClientMessageDto.SubscriptionToAllDropped>();
                                context.Log.Error("Subscription to ALL WAS DROPPED!");
                                break;
                            }
                            default:
                                context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                                break;
                        }
                    },
                    connectionClosed: (c, error) =>
                    {
                        if (error == SocketError.Success)
                            context.Success();
                        else
                            context.Fail();
                    });

            if (args.Length == 0)
            {
                context.Log.Info("SUBSCRIBING TO ALL STREAMS...");
                var corrid = Guid.NewGuid();
                var cmd = new ClientMessageDto.SubscribeToAllStreams(corrid);
                connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToAllStreams, corrid, cmd.Serialize()).AsByteArray());
            }
            else
            {
                foreach (var stream in args)
                {
                    context.Log.Info("SUBSCRIBING TO STREAM <{0}>...", stream);
                    var corrid = Guid.NewGuid();
                    var cmd = new ClientMessageDto.SubscribeToStream(corrid, stream);
                    connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToStream, corrid, cmd.Serialize()).AsByteArray());
                }
            }

            context.WaitForCompletion();
            return true;
        }
    }
}