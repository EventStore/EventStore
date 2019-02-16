using System;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	[TestFixture]
	public class when_truncating_database : SpecificationWithDirectoryPerTestFixture {
		[Test, Category("LongRunning")]
		public void everything_should_go_fine() {
			var miniNode = new MiniNode(PathName, inMemDb: false);
			miniNode.Start();

			var tcpPort = miniNode.TcpEndPoint.Port;
			var tcpSecPort = miniNode.TcpSecEndPoint.Port;
			var httpPort = miniNode.ExtHttpEndPoint.Port;
			const int cnt = 50;
			var countdown = new CountdownEvent(cnt);

			// --- first part of events
			WriteEvents(cnt, miniNode, countdown);
			Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Took too long writing first part of events.");
			countdown.Reset();

			// -- set up truncation
			var truncatePosition = miniNode.Db.Config.WriterCheckpoint.ReadNonFlushed();
			miniNode.Db.Config.TruncateCheckpoint.Write(truncatePosition);
			miniNode.Db.Config.TruncateCheckpoint.Flush();

			// --- second part of events
			WriteEvents(cnt, miniNode, countdown);
			Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Took too long writing second part of events.");
			countdown.Reset();

			miniNode.Shutdown(keepDb: true, keepPorts: true);

			// --- first restart and truncation
			miniNode = new MiniNode(PathName, tcpPort, tcpSecPort, httpPort, inMemDb: false);

			miniNode.Start();
			Assert.AreEqual(-1, miniNode.Db.Config.TruncateCheckpoint.Read());
			Assert.That(miniNode.Db.Config.WriterCheckpoint.Read(), Is.GreaterThanOrEqualTo(truncatePosition));

			// -- third part of events
			WriteEvents(cnt, miniNode, countdown);
			Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Took too long writing third part of events.");
			countdown.Reset();

			miniNode.Shutdown(keepDb: true, keepPorts: true);

			// -- second restart
			miniNode = new MiniNode(PathName, tcpPort, tcpSecPort, httpPort, inMemDb: false);
			Assert.AreEqual(-1, miniNode.Db.Config.TruncateCheckpoint.Read());
			miniNode.Start();

			// -- if we get here -- then everything is ok
			miniNode.Shutdown();
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void with_truncate_position_in_completed_chunk_everything_should_go_fine() {
			const int chunkSize = 1024 * 1024;
			const int cachedSize = chunkSize * 3;

			var miniNode = new MiniNode(PathName, chunkSize: chunkSize, cachedChunkSize: cachedSize, inMemDb: false);
			miniNode.Start();

			var tcpPort = miniNode.TcpEndPoint.Port;
			var tcpSecPort = miniNode.TcpSecEndPoint.Port;
			var httpPort = miniNode.ExtHttpEndPoint.Port;
			const int cnt = 1;
			var countdown = new CountdownEvent(cnt);

			// --- first part of events
			WriteEvents(cnt, miniNode, countdown, MiniNode.ChunkSize / 5 * 3);
			Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Took too long writing first part of events.");
			countdown.Reset();

			// -- set up truncation
			var truncatePosition = miniNode.Db.Config.WriterCheckpoint.ReadNonFlushed();
			miniNode.Db.Config.TruncateCheckpoint.Write(truncatePosition);
			miniNode.Db.Config.TruncateCheckpoint.Flush();

			// --- second part of events
			WriteEvents(cnt, miniNode, countdown, MiniNode.ChunkSize / 2);
			Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Took too long writing second part of events.");
			countdown.Reset();

			miniNode.Shutdown(keepDb: true, keepPorts: true);

			// --- first restart and truncation
			miniNode = new MiniNode(PathName, tcpPort, tcpSecPort, httpPort, chunkSize: chunkSize,
				cachedChunkSize: cachedSize, inMemDb: false);

			miniNode.Start();
			Assert.AreEqual(-1, miniNode.Db.Config.TruncateCheckpoint.Read());
			Assert.That(miniNode.Db.Config.WriterCheckpoint.Read(), Is.GreaterThanOrEqualTo(truncatePosition));

			// -- third part of events
			WriteEvents(cnt, miniNode, countdown, MiniNode.ChunkSize / 5);
			Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Took too long writing third part of events.");
			countdown.Reset();

			// -- if we get here -- then everything is ok
			miniNode.Shutdown();
		}

		private static void WriteEvents(int cnt, MiniNode miniNode, CountdownEvent countdown, int dataSize = 4000) {
			for (int i = 0; i < cnt; ++i) {
				miniNode.Node.MainQueue.Publish(
					new ClientMessage.WriteEvents(Guid.NewGuid(), Guid.NewGuid(),
						new CallbackEnvelope(m => {
							Assert.IsInstanceOf<ClientMessage.WriteEventsCompleted>(m);
							var msg = (ClientMessage.WriteEventsCompleted)m;
							Assert.AreEqual(OperationResult.Success, msg.Result);
							countdown.Signal();
						}),
						true,
						"test-stream",
						ExpectedVersion.Any,
						new[] {
							new Event(Guid.NewGuid(), "test-event-type", false, new byte[dataSize], null)
						},
						null));
			}
		}
	}
}
