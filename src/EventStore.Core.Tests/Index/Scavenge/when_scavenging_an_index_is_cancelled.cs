using System;
using System.IO;
using System.Threading;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Scavenge {
	[TestFixture]
	public class when_scavenging_an_index_is_cancelled : SpecificationWithDirectoryPerTestFixture {
		private PTable _oldTable;
		private string _expectedOutputFile;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var table = new HashListMemTable(PTableVersions.IndexV4, maxSize: 20);
			table.Add(0x010100000000, 0, 1);
			table.Add(0x010200000000, 0, 2);
			table.Add(0x010300000000, 0, 3);
			table.Add(0x010300000000, 1, 4);
			_oldTable = PTable.FromMemtable(table, GetTempFilePath());

			var cancellationTokenSource = new CancellationTokenSource();
			long spaceSaved;
			Func<IndexEntry, bool> existsAt = x => {
				cancellationTokenSource.Cancel();
				return true;
			};
			Func<IndexEntry, Tuple<string, bool>> readRecord = x => { throw new Exception("Should not be called"); };
			Func<string, ulong, ulong> upgradeHash = (streamId, hash) => {
				throw new Exception("Should not be called");
			};

			_expectedOutputFile = GetTempFilePath();
			Assert.That(
				() => PTable.Scavenged(_oldTable, _expectedOutputFile, upgradeHash, existsAt, readRecord,
					PTableVersions.IndexV4, out spaceSaved, ct: cancellationTokenSource.Token),
				Throws.InstanceOf<OperationCanceledException>());
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_oldTable.Dispose();

			base.TestFixtureTearDown();
		}

		[Test]
		public void the_output_file_is_deleted() {
			Assert.That(File.Exists(_expectedOutputFile), Is.False);
		}
	}
}
