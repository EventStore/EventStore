﻿using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class DeleteStream : RequestManagerBase {
		private readonly bool _hardDelete;
		private readonly CancellationToken _cancellationToken;
		private readonly string _streamId;

		public DeleteStream(
					IPublisher publisher,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					string streamId,
					long expectedVersion,
					bool hardDelete,
					CommitSource commitSource,
					CancellationToken cancellationToken = default)
			: base(
					 publisher,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion,
					 commitSource,
					 prepareCount: 0,
					 waitForCommit: true) {
			_hardDelete = hardDelete;
			_cancellationToken = cancellationToken;
			_streamId = streamId;
		}

		protected override Message WriteRequestMsg =>
			new StorageMessage.WriteDelete(
					InternalCorrId,
					WriteReplyEnvelope,
					_streamId,
					ExpectedVersion,
					_hardDelete,
					_cancellationToken);

		protected override Message ClientSuccessMsg =>
			 new ClientMessage.DeleteStreamCompleted(
				 ClientCorrId,
				 OperationResult.Success,
				 null,
				 CommitPosition,  //not technically correct, but matches current behavior correctly
				 CommitPosition);

		protected override Message ClientFailMsg =>
			new ClientMessage.DeleteStreamCompleted(ClientCorrId, Result, FailureMessage);
	}
}
