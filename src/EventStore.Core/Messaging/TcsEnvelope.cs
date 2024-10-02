// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;

namespace EventStore.Core.Messaging {
	public class TcsEnvelope<TResult> : IEnvelope where TResult : class {
		private readonly TaskCompletionSource<TResult> _tcs;
		public Task<TResult> Task => _tcs.Task;

		public TcsEnvelope() {
			_tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		public void ReplyWith<T>(T message) where T : Message {
			if (message is not TResult result) {
				_tcs.TrySetException(new ArgumentException("Unexpected message type.", nameof(message)));
				return;
			}

			_tcs.TrySetResult(result);
		}
	}
}
