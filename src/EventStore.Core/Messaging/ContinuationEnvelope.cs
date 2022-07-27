using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EventStore.Core.Messaging {
	public class ChannelEnvelope : IEnvelope {
		private readonly Channel<Message> _channel;

		public ChannelEnvelope(Channel<Message> channel) {
			_channel = channel;
		}

		public void ReplyWith<T>(T message) where T : Message {
			_channel.Writer.TryWrite(message);
		}
	}
	public class ContinuationEnvelope : IEnvelope {
		private readonly Func<Message, CancellationToken, Task> _onMessage;
		private readonly SemaphoreSlim _semaphore;
		private readonly CancellationToken _cancellationToken;

		private int _stopped;

		public ContinuationEnvelope(Func<Message, CancellationToken, Task> onMessage, SemaphoreSlim semaphore,
			CancellationToken cancellationToken) {
			_onMessage = onMessage;
			_semaphore = semaphore;
			_cancellationToken = cancellationToken;
		}

		public void StopReplies() {
			Interlocked.Or(ref _stopped, 1);
		}

		public void ReplyWith<T>(T message) where T : Message {
			try {
				do {
					if (Interlocked.And(ref _stopped, 1) == 1)
						return;
				} while (!_semaphore.Wait(50, _cancellationToken));
				_onMessage(message, _cancellationToken).ContinueWith(_ => _semaphore.Release(), _cancellationToken);
			}
			catch (ObjectDisposedException) {}
			catch (OperationCanceledException) {}
		}
	}
}
