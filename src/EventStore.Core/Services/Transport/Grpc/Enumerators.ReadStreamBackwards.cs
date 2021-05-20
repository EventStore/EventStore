using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public static partial class Enumerators {
		public class ReadStreamBackwards : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly string _streamName;
			private readonly ulong _maxCount;
			private readonly bool _resolveLinks;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly DateTime _deadline;
			private readonly Func<RpcException, Task> _onStreamNotFound;
			private readonly CancellationToken _cancellationToken;
			private readonly SemaphoreSlim _semaphore;
			private readonly Channel<ResolvedEvent> _channel;

			private ResolvedEvent _current;
			private ulong _readCount;

			public ResolvedEvent Current => _current;

			public ReadStreamBackwards(IPublisher bus,
				string streamName,
				StreamRevision startRevision,
				ulong maxCount,
				bool resolveLinks,
				ClaimsPrincipal user,
				bool requiresLeader,
				DateTime deadline,
				Func<RpcException, Task> onStreamNotFound,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (streamName == null) {
					throw new ArgumentNullException(nameof(streamName));
				}

				_bus = bus;
				_streamName = streamName;
				_maxCount = maxCount;
				_resolveLinks = resolveLinks;
				_user = user;
				_requiresLeader = requiresLeader;
				_deadline = deadline;
				_onStreamNotFound = onStreamNotFound;
				_cancellationToken = cancellationToken;
				_semaphore = new SemaphoreSlim(1, 1);
				_channel = Channel.CreateBounded<ResolvedEvent>(BoundedChannelOptions);

				ReadPage(startRevision);
			}


			public ValueTask DisposeAsync() {
				_channel.Writer.TryComplete();
				return new ValueTask(Task.CompletedTask);
			}

			public async ValueTask<bool> MoveNextAsync() {
				if (_readCount >= _maxCount) {
					return false;
				}

				if (!await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false)) {
					return false;
				}

				_current = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
				_readCount++;
				return true;
			}

			private void ReadPage(StreamRevision startRevision) {
				Guid correlationId = Guid.NewGuid();

				_bus.Publish(new ClientMessage.ReadStreamEventsBackward(
					correlationId, correlationId, new ContinuationEnvelope(OnMessage, _semaphore, _cancellationToken),
					_streamName, startRevision.ToInt64(), (int)Math.Min(ReadBatchSize, _maxCount), _resolveLinks,
					_requiresLeader, default, _user, _deadline));

				async Task OnMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
						RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						_channel.Writer.TryComplete(ex);
						return;
					}

					if (!(message is ClientMessage.ReadStreamEventsBackwardCompleted completed)) {
						_channel.Writer.TryComplete(
							RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsBackwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadStreamResult.Success:
							foreach (var @event in completed.Events) {
								await _channel.Writer.WriteAsync(@event, ct).ConfigureAwait(false);
							}

							if (completed.IsEndOfStream) {
								_channel.Writer.TryComplete();
								return;
							}

							ReadPage(StreamRevision.FromInt64(completed.NextEventNumber));
							return;
						case ReadStreamResult.NoStream:
							await _onStreamNotFound(RpcExceptions.StreamNotFound(_streamName)).ConfigureAwait(false);
							_channel.Writer.TryComplete();
							return;
						case ReadStreamResult.StreamDeleted:
							_channel.Writer.TryComplete(RpcExceptions.StreamDeleted(_streamName));
							return;
						case ReadStreamResult.AccessDenied:
							_channel.Writer.TryComplete(RpcExceptions.AccessDenied());
							return;
						default:
							_channel.Writer.TryComplete(RpcExceptions.UnknownError(completed.Result));
							return;
					}
				}
			}
		}
	}
}
