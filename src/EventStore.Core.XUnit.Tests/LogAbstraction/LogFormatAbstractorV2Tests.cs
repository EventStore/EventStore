using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogAbstraction {
	// checks the abstractor wires things up properly
	public class LogFormatAbstractorV2Tests :
		DirectoryPerTest<LogFormatAbstractorV2Tests>,
		IDisposable {

		private readonly DirectoryFixture<LogFormatAbstractorV2Tests> _fixture = new();
		private readonly LogFormatAbstractor<string> _sut;

		public LogFormatAbstractorV2Tests() {
			_sut = new LogV2FormatAbstractorFactory().Create(new() {
				IndexDirectory = _fixture.Directory,
				InMemory = false,
				StreamExistenceFilterSize = 1_000_000,
				StreamExistenceFilterCheckpoint = new InMemoryCheckpoint(),
			});
		}

		public void Dispose() {
			_sut.Dispose();
		}

		[Fact]
		public void can_init() {
			_sut.StreamExistenceFilter.Initialize(new MockExistenceFilterInitializer("1"), 0);
			Assert.True(_sut.StreamExistenceFilterReader.MightContain("1"));
		}

		[Fact]
		public void can_confirm() {
			_sut.StreamExistenceFilter.Initialize(new MockExistenceFilterInitializer(), 0);

			var prepare = LogRecord.SingleWrite(
				factory: _sut.RecordFactory,
				logPosition: 100,
				correlationId: Guid.NewGuid(),
				eventId: Guid.NewGuid(),
				eventStreamId: "streamA",
				expectedVersion: -1,
				eventType: "eventType",
				data: ReadOnlyMemory<byte>.Empty,
				metadata: ReadOnlyMemory<byte>.Empty);

			Assert.False(_sut.StreamExistenceFilterReader.MightContain("streamA"));

			_sut.StreamNameIndexConfirmer.Confirm(
				new[] { prepare },
				catchingUp: false,
				backend: null);

			Assert.True(_sut.StreamExistenceFilterReader.MightContain("streamA"));
		}
	}
}
