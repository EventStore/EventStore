// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Common;
using static EventStore.Core.Messages.ClientMessage;
using static EventStore.Core.Services.Transport.Enumerators.ReadResponse;

namespace EventStore.Core.Services.Transport.Enumerators;

partial class Enumerator {
	public class ReadStreamBackwards : IAsyncEnumerator<ReadResponse> {
		private readonly IPublisher _bus;
		private readonly string _streamName;
		private readonly ulong _maxCount;
		private readonly bool _resolveLinks;
		private readonly ClaimsPrincipal _user;
		private readonly bool _requiresLeader;
		private readonly DateTime _deadline;
		private readonly uint _compatibility;
		private readonly CancellationToken _cancellationToken;
		private readonly SemaphoreSlim _semaphore = new(1, 1);
		private readonly Channel<ReadResponse> _channel = Channel.CreateBounded<ReadResponse>(BoundedChannelOptions);

		private ReadResponse _current;

		public ReadResponse Current => _current;

		public ReadStreamBackwards(IPublisher bus,
			string streamName,
			StreamRevision startRevision,
			ulong maxCount,
			bool resolveLinks,
			ClaimsPrincipal user,
			bool requiresLeader,
			DateTime deadline,
			uint compatibility,
			CancellationToken cancellationToken) {
			_bus = Ensure.NotNull(bus);
			_streamName = Ensure.NotNullOrEmpty(streamName);
			_maxCount = maxCount;
			_resolveLinks = resolveLinks;
			_user = user;
			_requiresLeader = requiresLeader;
			_deadline = deadline;
			_compatibility = compatibility;
			_cancellationToken = cancellationToken;

			ReadPage(startRevision);
		}

		public ValueTask DisposeAsync() {
			_channel.Writer.TryComplete();
			return new ValueTask(Task.CompletedTask);
		}

		public async ValueTask<bool> MoveNextAsync() {
			if (!await _channel.Reader.WaitToReadAsync(_cancellationToken)) {
				return false;
			}

			_current = await _channel.Reader.ReadAsync(_cancellationToken);

			return true;
		}

		private void ReadPage(StreamRevision startRevision, ulong readCount = 0) {
			var correlationId = Guid.NewGuid();

			_bus.Publish(new ReadStreamEventsBackward(
				correlationId, correlationId, new ContinuationEnvelope(OnMessage, _semaphore, _cancellationToken),
				_streamName, startRevision.ToInt64(), (int)Math.Min(ReadBatchSize, _maxCount), _resolveLinks,
				_requiresLeader, null, _user, _deadline,
				cancellationToken: _cancellationToken)
			);

			async Task OnMessage(Message message, CancellationToken ct) {
				if (message is NotHandled notHandled && TryHandleNotHandled(notHandled, out var ex)) {
					_channel.Writer.TryComplete(ex);
					return;
				}

				if (message is not ReadStreamEventsBackwardCompleted completed) {
					_channel.Writer.TryComplete(ReadResponseException.UnknownMessage.Create<ReadStreamEventsBackwardCompleted>(message));
					return;
				}

				switch (completed.Result) {
					case ReadStreamResult.Success:
						foreach (var @event in completed.Events) {
							if (readCount >= _maxCount) {
								break;
							}
							await _channel.Writer.WriteAsync(new EventReceived(@event), ct);
							readCount++;
						}

						if (!completed.IsEndOfStream && readCount < _maxCount) {
							ReadPage(StreamRevision.FromInt64(completed.NextEventNumber), readCount);
							return;
						}

						if (_compatibility >= 1) {
							await _channel.Writer.WriteAsync(new LastStreamPositionReceived(StreamRevision.FromInt64(completed.LastEventNumber)), ct);
						}

						_channel.Writer.TryComplete();
						return;
					case ReadStreamResult.NoStream:
						await _channel.Writer.WriteAsync(new StreamNotFound(_streamName), ct);
						_channel.Writer.TryComplete();
						return;
					case ReadStreamResult.StreamDeleted:
						_channel.Writer.TryComplete(new ReadResponseException.StreamDeleted(_streamName));
						return;
					case ReadStreamResult.AccessDenied:
						_channel.Writer.TryComplete(new ReadResponseException.AccessDenied());
						return;
					default:
						_channel.Writer.TryComplete(ReadResponseException.UnknownError.Create(completed.Result));
						return;
				}
			}
		}
	}
}
