using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Tests.Services.Storage;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RedactionService {
	[TestFixture]
	public abstract class RedactionServiceTestFixture<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private SemaphoreSlim _switchChunkSemaphore;
		public RedactionService<TStreamId> RedactionService { get; private set; }

		public RedactionServiceTestFixture() : base(chunkSize: 512) { }

		[SetUp]
		public void SetUp() {
			_switchChunkSemaphore = new SemaphoreSlim(1, 1);
			RedactionService = new RedactionService<TStreamId>(Db, ReadIndex, _switchChunkSemaphore);
		}

		[TearDown]
		public void TearDown() {
			_switchChunkSemaphore?.Dispose();
		}

		protected static async Task<Message> Handle(Action<IEnvelope> handleMessage) {
			var tcs = new TaskCompletionSource<Message>();
			var envelope = new CallbackEnvelope(resultMsg => tcs.SetResult(resultMsg));
			handleMessage(envelope);
			return await tcs.Task;
		}
	}
}
