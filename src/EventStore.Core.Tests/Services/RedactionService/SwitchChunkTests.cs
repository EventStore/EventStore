using System.IO;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RedactionService {
	public abstract class SwitchChunkTests<TLogFormat, TStreamId> : RedactionServiceTestFixture<TLogFormat,TStreamId> {
		private const string StreamId = nameof(SwitchChunkTests<TLogFormat, TStreamId>);
		protected const string FakeChunk = "fake_chunk.tmp";

		protected override void WriteTestScenario() {
			// the writes below create 3 chunks for both log formats
			WriteSingleEvent(StreamId, 0, new string('0', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 1, new string('1', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 2, new string('2', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 3, new string('3', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 4, new string('4', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 5, new string('5', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 5, new string('6', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 5, new string('7', 50), retryOnFail: true);
			WriteSingleEvent(StreamId, 5, new string('8', 50), retryOnFail: true);

			var writerPos = Db.Config.WriterCheckpoint.Read();
			var chunk = Db.Manager.GetChunkFor(writerPos);
			var chunkNum = Db.Config.FileNamingStrategy.GetIndexFor(Path.GetFileName(chunk.FileName));
			Assert.AreEqual(2, chunkNum);

			using var fs = File.CreateText(Path.Combine(PathName, FakeChunk));
		}

		protected string GetChunk(int chunkNum, int chunkVersion, bool fullPath = false) {
			var chunkPath = Db.Config.FileNamingStrategy.GetFilenameFor(chunkNum, chunkVersion);
			return fullPath ? chunkPath : Path.GetFileName(chunkPath);
		}

		protected async Task<RedactionMessage.SwitchChunkCompleted> SwitchChunk(string targetChunk, string newChunk) {
			return await Handle(e =>
					RedactionService.Handle(new RedactionMessage.SwitchChunk(e, targetChunk, newChunk)))
				as RedactionMessage.SwitchChunkCompleted;
		}
	}
}
