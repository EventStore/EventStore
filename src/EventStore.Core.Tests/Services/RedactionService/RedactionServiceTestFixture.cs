using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Synchronization;
using EventStore.Core.Tests.Services.Storage;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RedactionService {
	[TestFixture]
	public abstract class RedactionServiceTestFixture<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private SemaphoreSlimLock _switchChunksLock;
		public RedactionService<TStreamId> RedactionService { get; private set; }

		public RedactionServiceTestFixture() : base(chunkSize: 1024) { }

		[SetUp]
		public void SetUp() {
			_switchChunksLock = new SemaphoreSlimLock();
			RedactionService = new RedactionService<TStreamId>(Db, ReadIndex, _switchChunksLock);
		}

		[TearDown]
		public void TearDown() {
			_switchChunksLock?.Dispose();
		}

		protected static async Task<Message> CallbackResult(Action<CallbackEnvelope> action) {
			var tcs = new TaskCompletionSource<Message>();
			var envelope = new CallbackEnvelope(resultMsg => tcs.SetResult(resultMsg));
			action(envelope);
			return await tcs.Task;
		}
	}
}
