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
                var cmd = new ClientMessageDto.SubscribeToAllStreams();
                connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToAllStreams, Guid.NewGuid(), cmd.Serialize()).AsByteArray());
            }
            else
            {
                foreach (var stream in args)
                {
                    context.Log.Info("SUBSCRIBING TO STREAM <{0}>...", stream);
                    var cmd = new ClientMessageDto.SubscribeToStream(stream);
                    connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToStream, Guid.NewGuid(), cmd.Serialize()).AsByteArray());
                }
            }

            context.WaitForCompletion();
            return true;
        }
    }
}