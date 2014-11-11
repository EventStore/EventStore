using System;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;

namespace EventStore.CompetingPlayground
{
    public class Program
    {
        private const string Stream = "stream";
        private const string SubName = "group";

        private static readonly PersistentSubscriptionSettings Settings = PersistentSubscriptionSettingsBuilder.Create()
            .DoNotResolveLinkTos()
            .StartFromBeginning()
            .WithExtraStatistics()
            .WithMessageTimeoutOf(TimeSpan.FromMilliseconds(5000))
            .MinimumCheckPointCountOf(100)
            .MaximumCheckPointCountOf(500)
            .CheckPointAfter(TimeSpan.FromSeconds(2));

        static void Main()
        {
            BasicTest();
        }

        private static void BasicTest()
        {
            var localEndpoint = new IPEndPoint(IPAddress.Loopback, 1113);
            var connSettings = ConnectionSettings.Create().PerformOnMasterOnly();
            //var clusterSettings = ClusterSettings.Create()
            //    .DiscoverClusterViaGossipSeeds()
            //    .SetMaxDiscoverAttempts(10)
            //    .SetGossipSeedEndPoints(new GossipSeed(new IPEndPoint(IPAddress.Loopback, 1113)),
            //        new GossipSeed(new IPEndPoint(IPAddress.Loopback, 2113)),
            //        new GossipSeed(new IPEndPoint(IPAddress.Loopback, 3113)));

	    using (var connection = EventStoreConnection.Create(connSettings, localEndpoint))
            {
                connection.ConnectAsync().Wait();

                //WriteEvents(connection, 1000);
                CreateSubscription(connection, SubName);

                var sub = ConnectToSubscription(connection, "sub1");
                var sub2 = ConnectToSubscription(connection, "sub2");
                WriteEvents(connection, 50000);
                sub.Stop(TimeSpan.FromSeconds(5));
                sub2.Stop(TimeSpan.FromSeconds(5));
                Thread.Sleep(TimeSpan.FromSeconds(5));
                DeleteSubscription(connection, SubName);
            }
        }


        private static EventStorePersistentSubscription ConnectToSubscription(IEventStoreConnection connection, string name)
        {
            return connection.ConnectToPersistentSubscription(SubName, Stream,
                (sub, ev) =>
                {
                    Thread.Sleep(100);
                    Console.WriteLine("acking " + ev.OriginalEventNumber);
                    sub.Acknowledge(ev);
                },
                (sub, ev, ex) => Console.WriteLine(name + "sub dropped " + ev),
                bufferSize: 10, autoAck: false);
        }

        private static void WriteEvents(IEventStoreConnection connection, int count)
        {
            for (var i = 0; i < count; i++)
            {
                connection.AppendToStreamAsync(Stream, ExpectedVersion.Any, new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
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
                connection.CreatePersistentSubscriptionAsync(Stream, name, Settings, new UserCredentials("admin", "changeit")).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to create : " + ex);
            }
        }

    }
}