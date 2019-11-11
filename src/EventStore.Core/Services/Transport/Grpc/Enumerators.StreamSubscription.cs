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
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class StreamSubscription : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly string _streamName;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;

			private StreamRevision _nextRevision;
			private ResolvedEvent _current;

			public ResolvedEvent Current => _current;

			public StreamSubscription(IPublisher bus,
				string streamName,
				StreamRevision startRevision,
				bool resolveLinks,
				IPrincipal user,
				IReadIndex readIndex,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (streamName == null) {
					throw new ArgumentNullException(nameof(streamName));
				}

				if (readIndex == null) throw new ArgumentNullException(nameof(readIndex));

				_bus = bus;
				_streamName = streamName;
				_nextRevision = startRevision;
				_resolveLinks = resolveLinks;
				_user = user;
				_readIndex = readIndex;
				_disposedTokenSource = new CancellationTokenSource();
				_buffer = new ConcurrentQueue<ResolvedEvent>();
				cancellationToken.Register(_disposedTokenSource.Dispose);
			}


			public ValueTask DisposeAsync() {
				_disposedTokenSource.Dispose();
				return default;
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				if (_buffer.TryDequeue(out var current)) {
					_current = current;
					return true;
				}

				var correlationId = Guid.NewGuid();

				var readNextSource = new TaskCompletionSource<bool>();

				var nextRevision = _nextRevision == StreamRevision.End
					? Math.Max(_readIndex.GetStreamLastEventNumber(_streamName), 0L)
					: _nextRevision.ToInt64();

				_bus.Publish(new ClientMessage.ReadStreamEventsForward(
					correlationId, correlationId, new CallbackEnvelope(OnMessage), _streamName,
					nextRevision, 32,
					_resolveLinks, false, default, _user));

				await readNextSource.Task;

				if (_buffer.TryDequeue(out current)) {
					_current = current;
					return true;
				}

				await Task.Delay(100, _disposedTokenSource.Token);

				goto ReadLoop;

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
