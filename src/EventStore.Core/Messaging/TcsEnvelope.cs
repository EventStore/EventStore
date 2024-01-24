using System;
using System.Threading.Tasks;

namespace EventStore.Core.Messaging {
	public class TcsEnvelope<TResult> : IEnvelope where TResult : class {
		private readonly TaskCompletionSource<TResult> _tcs;
		public Task<TResult> Task => _tcs.Task;

		public TcsEnvelope() {
			_tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		public void ReplyWith<T>(T message) where T : class, Message {
			if (message is not TResult result) {
				_tcs.TrySetException(new ArgumentException("Unexpected message type.", nameof(message)));
				return;
			}

			_tcs.TrySetResult(result);
		}
	}
}
