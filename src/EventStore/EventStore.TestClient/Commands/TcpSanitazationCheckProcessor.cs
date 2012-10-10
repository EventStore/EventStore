using System;
using EventStore.Common.Log;
using EventStore.Core.Services.Transport.Tcp;
using System.Linq;

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
                                          (byte) TcpCommand.Ping,
                                          (byte) TcpCommand.CreateStream,
                                          (byte) TcpCommand.WriteEvents,
                                          (byte) TcpCommand.TransactionStart,
                                          (byte) TcpCommand.TransactionWrite,
                                          (byte) TcpCommand.TransactionCommit,
                                          (byte) TcpCommand.DeleteStream,
                                          (byte) TcpCommand.ReadEvent,
                                          (byte) TcpCommand.ReadEventsForward,
                                          (byte) TcpCommand.ReadEventsFromEnd,
                                          (byte) TcpCommand.SubscribeToStream,
                                          (byte) TcpCommand.UnsubscribeFromStream,
                                          (byte) TcpCommand.SubscribeToAllStreams,
                                          (byte) TcpCommand.UnsubscribeFromAllStreams,
                                          (byte) TcpCommand.StreamEventAppeared,
                                          (byte) TcpCommand.SubscriptionDropped,
                                          (byte) TcpCommand.SubscriptionToAllDropped,
                                          (byte) TcpCommand.ScavengeDatabase
                                      };

            context.Client.CreateTcpConnection(
            context, 
            (connection, package) =>
            {
                context.Fail(null, "Received stuff after sending invalid command");
            },
            connection =>
            {
                foreach (var command in commandsToCkeck)
                {
                    connection.EnqueueSend(new TcpPackage((TcpCommand)command, Guid.NewGuid(), new byte[] { 0, 1, 0, 1 }).AsByteArray());
                }

                //sent some bytes test
                connection.EnqueueSend(BitConverter.GetBytes(int.MaxValue).Union(new byte[]{1,2,3,4}).ToArray());
            },
            (connection, error) =>
            {

            });

            Log.Info("Sent [{0}] and received no package in response, which means server survived",
                     string.Join(",", commandsToCkeck.Select(c => ((TcpCommand) c).ToString())));
            context.Success();
            return true;
        }
    }
}
