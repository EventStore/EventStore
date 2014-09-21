//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reactive.Disposables;
//using System.Reactive.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Net.Sockets;
//using System.Net;
//using EventStore.ClientAPI;
//using EventStore.ClientAPI.SystemData;
//namespace ConsoleApplication5
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            Task.Run(() => MainAsync(args)).Wait();
//        }

//        public static async Task MainAsync(string[] args)
//        {
//            var TheStreamName = "CompetingTestStream";
//            var address = Dns.GetHostAddresses("localhost").First(e => e.AddressFamily == AddressFamily.InterNetwork);
//            var ipep = new IPEndPoint(address, 1113);
//            // used for writing
//            var conn1 = EventStoreConnection.Create(ipep);
//            conn1.ConnectAsync().Wait();
//            // used for first consumer
//            var conn2 = EventStoreConnection.Create(ipep);
//            conn2.ConnectAsync().Wait();
//            // used for third consumer
//            var conn3 = EventStoreConnection.Create(ipep);
//            conn3.ConnectAsync().Wait();
//            // before any subscribers connect, write out 25 events
//            for (int i = 0; i < 25; i++)
//            {
//                WriteEvent(conn1, TheStreamName, "message:" + i);
//            }
//            // Get the two observable subscriptions
//            var subscriber1 = ObserveCompeting(TheStreamName, "subscriber1", conn2);
//            var subscriber2 = ObserveCompeting(TheStreamName, "subscriber2", conn3);
//            subscriber1.Subscribe(msg => Console.WriteLine("Subscriber 1 received {0}", msg));
//            subscriber2.Subscribe(msg => Console.WriteLine("Subscriber 2 received {0}", msg));
//            Console.WriteLine("Press a letter or number to push to stream...");
//            ConsoleKeyInfo keyIn;
//            do
//            {
//                keyIn = Console.ReadKey();
//                if (Char.IsLetterOrDigit(keyIn.KeyChar))
//                {
//                    var asChar = keyIn.KeyChar;
//                    WriteEvent(conn1, TheStreamName, new String(asChar, 1));
//                }
//            } while (keyIn.Key != ConsoleKey.Escape);
//            Console.ReadLine();
            
//            conn1.Dispose();
//            conn2.Dispose();
//            conn3.Dispose();
//        }

//        public static void WriteEvent(IEventStoreConnection connection, string streamName, string data)
//        {
//            Console.WriteLine("Writing '{0}' to {1}...", data, streamName);
//            var evtData = new EventData(Guid.NewGuid(), "event", false, Encoding.UTF8.GetBytes(data), new byte[0]);
//            connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, evtData).Wait();
//        }
//        public static IObservable<string> ObserveCompeting(string streamName, string queueName, IEventStoreConnection connection)
//        {
//            return Observable.Create<string>(obs =>
//            {
//                Action<EventStorePersistentSubscription, ResolvedEvent> onEvent = (sub, ev) =>
//                {
//                    var payload = Encoding.UTF8.GetString(ev.Event.Data);
//                    obs.OnNext(payload);
//                    sub.Acknowledge(ev);
//                };
//                // make sure subscription exists
//                var builder = PersistentSubscriptionSettingsBuilder.Create().StartFromBeginning();
//                var creds = new UserCredentials("admin", "changeit");
//                try
//                {
//                    connection.CreatePersistentSubscriptionAsync(streamName, queueName, builder, creds).Wait();
//                }
//                catch (Exception e)
//                {
//                    // likely sub already exists
//                    Console.WriteLine("Create sub error:" + e.Message);
//                }
//                var subscription = connection.ConnectToPersistentSubscription(queueName, streamName, onEvent);
//                Console.WriteLine("Subscription to stream {0}, group {1} connected", streamName, queueName);
//                return Disposable.Create(() => subscription.Stop(TimeSpan.FromSeconds(15)));
//            });
//        }
//    }
//}



using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;

namespace CompetingPlayground
{
    class Program
    {
        private static readonly string Stream = "hhhhhhhhhhh";
        private static readonly string SubName = "greG";

        private static readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
            .DoNotResolveLinkTos()
            .StartFromBeginning()
            .WithExtraLatencyStatistics()
            .WithMessageTimeoutOf(TimeSpan.FromMilliseconds(5000))
            .MinimumCheckPointCountOf(100)
            .MaximumCheckPointCountOf(500)
            .CheckPointAfter(TimeSpan.FromSeconds(2));

        static void Main(string[] args)
        {
            BasicTest();
        }

        private static void BasicTest()
        {
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113);
            using (var connection = EventStoreConnection.Create(endpoint, "foo"))
            {
                connection.ConnectAsync().Wait();

                //WriteEvents(connection);
                CreateSubscription(connection, SubName);

                var sub = ConnectToSubscription(connection, "sub1");
                var sub2 = ConnectToSubscription(connection, "sub2");
                Console.WriteLine("delaying.");
                Task.Delay(1000).Wait();
                WriteEvents(connection);
                sub.Stop(TimeSpan.FromSeconds(5));
                sub2.Stop(TimeSpan.FromSeconds(5));
                Thread.Sleep(TimeSpan.FromSeconds(5));
                //DeleteSubscription(connection, SubName);
            }
        }


        private static EventStorePersistentSubscription ConnectToSubscription(IEventStoreConnection connection, string name)
        {
            return connection.ConnectToPersistentSubscription(SubName, Stream,
                (sub, ev) =>
                {
                    Console.WriteLine(name + "received: " + ev.OriginalEventNumber);
                    sub.Acknowledge(ev);
                },
                (sub, ev, ex) => Console.WriteLine(name + "sub dropped " + ev),
                bufferSize: 200, autoAck: false);
        }

        private static void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 1000; i++)
            {
                connection.AppendToStreamAsync(Stream, ExpectedVersion.Any,
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        private static void DeleteSubscription(IEventStoreConnection connection, string name)
        {
            try
            {
                connection.DeletePersistentSubscriptionAsync(Stream, name, new UserCredentials("admin", "changeit")).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to delete : " + ex);
            }
        }


        private static void CreateSubscription(IEventStoreConnection connection, string name)
        {
            try
            {
                connection.CreatePersistentSubscriptionAsync(Stream, name, _settings,
                    new UserCredentials("admin", "changeit")).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to create : " + ex);
            }
        }

    }
}
