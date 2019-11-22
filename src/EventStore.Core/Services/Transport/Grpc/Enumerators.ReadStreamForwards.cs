using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class ReadStreamForwards : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly string _streamName;
			private readonly int _maxCount;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;
			private StreamRevision _nextRevision;
			private bool _isEnd;
			private ResolvedEvent _current;
			private int _readCount;
			public ResolvedEvent Current => _current;

			public ReadStreamForwards(
				IPublisher bus,
				string streamName,
				StreamRevision startRevision,
				int maxCount,
				bool resolveLinks,
				IPrincipal user,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (streamName == null) {
					throw new ArgumentNullException(nameof(streamName));
				}

				_bus = bus;
				_streamName = streamName;
				_nextRevision = startRevision;
				_maxCount = maxCount;
				_resolveLinks = resolveLinks;
				_user = user;
				_disposedTokenSource = new CancellationTokenSource();
				_buffer = new ConcurrentQueue<ResolvedEvent>();
				cancellationToken.Register(_disposedTokenSource.Dispose);
			}

			public ValueTask DisposeAsync() {
				_disposedTokenSource.Dispose();
				return default;
			}

			public async ValueTask<bool> MoveNextAsync() {
				if (_readCount >= _maxCount) {
					return false;
				}

				if (_buffer.TryDequeue(out var current)) {
					_current = current;
					_readCount++;
					return true;
				}

				if (_isEnd) {
					return false;
				}

				var readNextSource = new TaskCompletionSource<bool>();

				var correlationId = Guid.NewGuid();

				_bus.Publish(new ClientMessage.ReadStreamEventsForward(
					correlationId, correlationId, new CallbackEnvelope(OnMessage), _streamName,
					_nextRevision.ToInt64(), Math.Min(_maxCount, 32),
					_resolveLinks, false, default, _user));

				if (!await readNextSource.Task) {
					return false;
				}

				if (_buffer.TryDequeue(out current)) {
					_current = current;
					_readCount++;
					return true;
				}

				return false;

				void OnMessage(Message message) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						readNextSource.TrySetException(ex);
						return;
					}

					if (!(message is ClientMessage.ReadStreamEventsForwardCompleted completed)) {
						readNextSource.TrySetException(
							RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadStreamResult.Success:
							foreach (var @event in completed.Events) {
								_buffer.Enqueue(@event);
							}

							_isEnd = completed.IsEndOfStream;
							_nextRevision = StreamRevision.FromInt64(completed.NextEventNumber);
							readNextSource.TrySetResult(true);
							return;
						case ReadStreamResult.NoStream:
							readNextSource.TrySetException(RpcExceptions.StreamNotFound(_streamName));
							return;
						case ReadStreamResult.StreamDeleted:
							readNextSource.TrySetException(RpcExceptions.StreamDeleted(_streamName));
							return;
						case ReadStreamResult.AccessDenied:
							readNextSource.TrySetException(RpcExceptions.AccessDenied());
							return;
						default:
							readNextSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
							return;
					}
				}
			}
		}
	}
}
