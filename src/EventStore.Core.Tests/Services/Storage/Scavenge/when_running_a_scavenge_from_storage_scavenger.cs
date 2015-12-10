using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.UserManagement;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
	public class when_running_scavenge_from_storage_scavenger : TestFixtureWithExistingEvents
    {
        protected EventStore.Core.Services.Storage.StorageScavenger _scavenger;
        private ManualResetEvent _hasCompletedScavenge = new ManualResetEvent(false);

        protected override void Given()
        {
            base.Given();

			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			var pathName = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", Guid.NewGuid(), typeName));
			Directory.CreateDirectory(pathName);

        	var db = new TFChunkDb(new TFChunkDbConfig(pathName,
			                                        new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
			                                        16 * 1024,
			                                        0,
			                                        new InMemoryCheckpoint(),
			                                        new InMemoryCheckpoint(),
			                                        new InMemoryCheckpoint(-1),
			                                        new InMemoryCheckpoint(-1)));
			db.Open();
			_scavenger = new StorageScavenger(db, _ioDispatcher, new FakeTableIndex(), new ByLengthHasher(),
			                                  new FakeReadIndex(x => x == "es-to-scavenge"), true, "fakeNodeIp", true, 30);

            var scavengeMessage = new ClientMessage.ScavengeDatabase(Envelope, Guid.NewGuid(), SystemAccount.Principal);
            _bus.Subscribe<ClientMessage.ScavengeDatabaseCompleted>(new AdHocHandler<ClientMessage.ScavengeDatabaseCompleted>(
                           x => {_hasCompletedScavenge.Set();}));
            _scavenger.Handle(scavengeMessage);
        }

        [Test]
        public void creates_scavenge_started_events_in_both_streams()
        {
            Assert.IsTrue(_hasCompletedScavenge.WaitOne(TimeSpan.FromSeconds(5)));

            var scavengeInstanceStreamResults = GetMessagesFromScavengeInstanceStream();
            var scavengeEventResults = GetMessagesFromScavengesStream();

        	Assert.AreEqual(1, scavengeInstanceStreamResults.Count(x => x.Events.Any(y => y.EventType == SystemEventTypes.ScavengeStarted)));
            Assert.AreEqual(1, scavengeEventResults.Count(x => x.Events.Any(y => y.EventType == SystemEventTypes.ScavengeStarted)));
        }

        [Test]
        public void creates_scavenge_completed_events_in_both_streams()
        {
            Assert.IsTrue(_hasCompletedScavenge.WaitOne(TimeSpan.FromSeconds(5)));

            var scavengeInstanceStreamResults = GetMessagesFromScavengeInstanceStream();
            var scavengeEventResults = GetMessagesFromScavengesStream();

        	Assert.AreEqual(1, scavengeInstanceStreamResults.Count(x => x.Events.Any(y => y.EventType == SystemEventTypes.ScavengeCompleted)));
        	Assert.AreEqual(1, scavengeEventResults.Count(x => x.Events.Any(y => y.EventType == SystemEventTypes.ScavengeCompleted)));
        }

        [Test]
        public void creates_a_metadata_stream()
        {
            Assert.IsTrue(_hasCompletedScavenge.WaitOne(TimeSpan.FromSeconds(5)));

            var metadataMessages = HandledMessages.OfType<ClientMessage.WriteEvents>()
                    .Where(v=>
                            v.EventStreamId.StartsWith(
                                Core.Services.SystemStreams.MetastreamOf(Core.Services.SystemStreams.ScavengesStream) + "-")
                    ).ToList<ClientMessage.WriteEvents>();

            Assert.AreEqual(1, metadataMessages.Count());
        }

        protected List<ClientMessage.WriteEvents> GetMessagesFromScavengeInstanceStream() {
            return HandledMessages.OfType<ClientMessage.WriteEvents>()
    				.Where(v =>
    				        v.EventStreamId.StartsWith(Core.Services.SystemStreams.ScavengesStream + "-")
    		        ).ToList<ClientMessage.WriteEvents>();
        }

        protected List<ClientMessage.WriteEvents> GetMessagesFromScavengesStream() {
            return HandledMessages.OfType<ClientMessage.WriteEvents>()
                    .Where(v =>
                           v.EventStreamId.StartsWith(Core.Services.SystemStreams.ScavengesStream))
                    .ToList<ClientMessage.WriteEvents>();
        }
    }
}
