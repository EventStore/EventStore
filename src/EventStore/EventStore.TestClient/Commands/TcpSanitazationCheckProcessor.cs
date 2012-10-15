using System;
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
                                          (byte) TcpCommand.CreateStream,
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
                                          (byte) TcpCommand.SubscribeToAllStreams,
                                          (byte) TcpCommand.UnsubscribeFromAllStreams,
                                          (byte) TcpCommand.StreamEventAppeared,
                                          (byte) TcpCommand.SubscriptionDropped,
                                          (byte) TcpCommand.SubscriptionToAllDropped,
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

                var connection = context.Client.CreateTcpConnection(context,
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
            context.Success();
            return true;
        }
    }
}
