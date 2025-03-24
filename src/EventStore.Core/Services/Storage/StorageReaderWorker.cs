// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.InMemory;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using static EventStore.Core.Messages.ClientMessage;
using ILogger = Serilog.ILogger;

// ReSharper disable StaticMemberInGenericType

namespace EventStore.Core.Services.Storage;

public abstract class StorageReaderWorker {
	protected static readonly ILogger Log = Serilog.Log.ForContext<StorageReaderWorker>();
}

public class StorageReaderWorker<TStreamId>(
	IPublisher publisher,
	IReadIndex<TStreamId> readIndex,
	ISystemStreamLookup<TStreamId> systemStreams,
	IReadOnlyCheckpoint writerCheckpoint,
	IInMemoryStreamReader inMemReader,
	int queueId)
	:
		StorageReaderWorker,
		IAsyncHandle<ReadEvent>,
		IAsyncHandle<ReadStreamEventsBackward>,
		IAsyncHandle<ReadStreamEventsForward>,
		IAsyncHandle<ReadAllEventsForward>,
		IAsyncHandle<ReadAllEventsBackward>,
		IAsyncHandle<FilteredReadAllEventsForward>,
		IAsyncHandle<FilteredReadAllEventsBackward>,
		IAsyncHandle<StorageMessage.EffectiveStreamAclRequest>,
		IAsyncHandle<StorageMessage.StreamIdFromTransactionIdRequest>,
		IHandle<StorageMessage.BatchLogExpiredMessages> {
	private static readonly ResolvedEvent[] EmptyRecords = [];
	private static readonly char[] LinkToSeparator = ['@'];

	private readonly IReadIndex<TStreamId> _readIndex = Ensure.NotNull(readIndex);
	private readonly ISystemStreamLookup<TStreamId> _systemStreams = Ensure.NotNull(systemStreams);
	private readonly IReadOnlyCheckpoint _writerCheckpoint = Ensure.NotNull(writerCheckpoint);
	private const int MaxPageSize = 4096;
	private DateTime? _lastExpireTime;
	private long _expiredBatchCount;
	private bool _batchLoggingEnabled;

	async ValueTask IAsyncHandle<ReadEvent>.HandleAsync(ReadEvent msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			if (LogExpiredMessage(msg.Expires))
				Log.Debug(
					"Read Event operation has expired for Stream: {stream}, Event Number: {eventNumber}. Operation Expired at {expiryDateTime}",
					msg.EventStreamId, msg.EventNumber, msg.Expires);
			return;
		}

		var cts = token.LinkTo(msg.CancellationToken);
		try {
			var ev = await ReadEvent(msg, token);
			msg.Envelope.ReplyWith(ev);
		} catch (OperationCanceledException ex) when (ex.CancellationToken == cts?.Token) {
			throw new OperationCanceledException(null, ex, cts.CancellationOrigin);
		} finally {
			cts?.Dispose();
		}
	}

	async ValueTask IAsyncHandle<ReadStreamEventsForward>.HandleAsync(ReadStreamEventsForward msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			if (msg.ReplyOnExpired) {
				msg.Envelope.ReplyWith(new ReadStreamEventsForwardCompleted(
					msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
					ReadStreamResult.Expired,
					ResolvedEvent.EmptyArray, default, default, default, default, default, default, default));
			}

			if (LogExpiredMessage(msg.Expires))
				Log.Debug(
					"Read Stream Events Forward operation has expired for Stream: {stream}, From Event Number: {fromEventNumber}, Max Count: {maxCount}. Operation Expired at {expiryDateTime}",
					msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, msg.Expires);
			return;
		}

		ReadStreamEventsForwardCompleted res;
		var cts = token.LinkTo(msg.CancellationToken);
		try {
			res = SystemStreams.IsInMemoryStream(msg.EventStreamId)
				? inMemReader.ReadForwards(msg)
				: await ReadStreamEventsForward(msg, token);
		} catch (OperationCanceledException ex) when (ex.CancellationToken == cts?.Token) {
			throw new OperationCanceledException(null, ex, cts.CancellationOrigin);
		} finally {
			cts?.Dispose();
		}

		switch (res.Result) {
			case ReadStreamResult.Success:
			case ReadStreamResult.NoStream:
			case ReadStreamResult.NotModified:
				if (msg.LongPollTimeout.HasValue && res.FromEventNumber > res.LastEventNumber) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						msg.EventStreamId, res.TfLastCommitPosition, res.LastEventNumber,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else {
					msg.Envelope.ReplyWith(res);
				}

				break;
			case ReadStreamResult.StreamDeleted:
			case ReadStreamResult.Error:
			case ReadStreamResult.AccessDenied:
				msg.Envelope.ReplyWith(res);
				break;
			default:
				throw new ArgumentOutOfRangeException(
					$"Unknown ReadStreamResult: {res.Result}");
		}
	}

	async ValueTask IAsyncHandle<ReadStreamEventsBackward>.HandleAsync(ReadStreamEventsBackward msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			if (LogExpiredMessage(msg.Expires))
				Log.Debug(
					"Read Stream Events Backward operation has expired for Stream: {stream}, From Event Number: {fromEventNumber}, Max Count: {maxCount}. Operation Expired at {expiryDateTime}",
					msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, msg.Expires);
			return;
		}

		var cts = token.LinkTo(msg.CancellationToken);
		try {
			var res = SystemStreams.IsInMemoryStream(msg.EventStreamId)
				? inMemReader.ReadBackwards(msg)
				: await ReadStreamEventsBackward(msg, token);

			msg.Envelope.ReplyWith(res);
		} catch (OperationCanceledException ex) when (ex.CancellationToken == cts?.Token) {
			throw new OperationCanceledException(null, ex, cts.CancellationOrigin);
		} finally {
			cts?.Dispose();
		}
	}

	async ValueTask IAsyncHandle<ReadAllEventsForward>.HandleAsync(ReadAllEventsForward msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			if (msg.ReplyOnExpired) {
				msg.Envelope.ReplyWith(new ReadAllEventsForwardCompleted(
					msg.CorrelationId, ReadAllResult.Expired,
					default, ResolvedEvent.EmptyArray, default, default, default,
					currentPos: new TFPos(msg.CommitPosition, msg.PreparePosition),
					TFPos.Invalid, TFPos.Invalid, default));
			}

			if (LogExpiredMessage(msg.Expires))
				Log.Debug(
					"Read All Stream Events Forward operation has expired for C:{commitPosition}/P:{preparePosition}. Operation Expired at {expiryDateTime}",
					msg.CommitPosition, msg.PreparePosition, msg.Expires);
			return;
		}

		var res = await ReadAllEventsForward(msg, token);
		switch (res.Result) {
			case ReadAllResult.Success:
				if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.Events.Count is 0) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else
					msg.Envelope.ReplyWith(res);

				break;
			case ReadAllResult.NotModified:
				if (msg.LongPollTimeout.HasValue && res.IsEndOfStream &&
				    res.CurrentPos.CommitPosition > res.TfLastCommitPosition) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else
					msg.Envelope.ReplyWith(res);

				break;
			case ReadAllResult.Error:
			case ReadAllResult.AccessDenied:
			case ReadAllResult.InvalidPosition:
				msg.Envelope.ReplyWith(res);
				break;
			default:
				throw new ArgumentOutOfRangeException($"Unknown ReadAllResult: {res.Result}");
		}
	}

	async ValueTask IAsyncHandle<ReadAllEventsBackward>.HandleAsync(ReadAllEventsBackward msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			if (LogExpiredMessage(msg.Expires))
				Log.Debug(
					"Read All Stream Events Backward operation has expired for C:{commitPosition}/P:{preparePosition}. Operation Expired at {expiryDateTime}",
					msg.CommitPosition, msg.PreparePosition, msg.Expires);
			return;
		}

		msg.Envelope.ReplyWith(await ReadAllEventsBackward(msg, token));
	}

	async ValueTask IAsyncHandle<FilteredReadAllEventsForward>.HandleAsync(FilteredReadAllEventsForward msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			if (msg.ReplyOnExpired) {
				msg.Envelope.ReplyWith(new FilteredReadAllEventsForwardCompleted(
					msg.CorrelationId, FilteredReadAllResult.Expired,
					default, ResolvedEvent.EmptyArray, default, default, default,
					currentPos: new TFPos(msg.CommitPosition, msg.PreparePosition),
					TFPos.Invalid, TFPos.Invalid, default, default, default));
			}

			Log.Debug(
				"Read All Stream Events Forward Filtered operation has expired for C:{0}/P:{1}. Operation Expired at {2}",
				msg.CommitPosition, msg.PreparePosition, msg.Expires);
			return;
		}

		var res = await FilteredReadAllEventsForward(msg, token);
		switch (res.Result) {
			case FilteredReadAllResult.Success:
				if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.Events.Count is 0) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else
					msg.Envelope.ReplyWith(res);

				break;
			case FilteredReadAllResult.NotModified:
				if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.CurrentPos.CommitPosition > res.TfLastCommitPosition) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else
					msg.Envelope.ReplyWith(res);

				break;
			case FilteredReadAllResult.Error:
			case FilteredReadAllResult.AccessDenied:
			case FilteredReadAllResult.InvalidPosition:
				msg.Envelope.ReplyWith(res);
				break;
			default:
				throw new ArgumentOutOfRangeException($"Unknown ReadAllResult: {res.Result}");
		}
	}

	async ValueTask IAsyncHandle<FilteredReadAllEventsBackward>.HandleAsync(FilteredReadAllEventsBackward msg, CancellationToken token) {
		if (msg.CancellationToken.IsCancellationRequested)
			return;

		if (msg.Expires < DateTime.UtcNow) {
			Log.Debug(
				"Read All Stream Events Backward Filtered operation has expired for C:{0}/P:{1}. Operation Expired at {2}",
				msg.CommitPosition, msg.PreparePosition, msg.Expires);
			return;
		}

		var res = await FilteredReadAllEventsBackward(msg, token);
		switch (res.Result) {
			case FilteredReadAllResult.Success:
				if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.Events.Count is 0) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else
					msg.Envelope.ReplyWith(res);

				break;
			case FilteredReadAllResult.NotModified:
				if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.CurrentPos.CommitPosition > res.TfLastCommitPosition) {
					publisher.Publish(new SubscriptionMessage.PollStream(
						SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
						DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
				} else
					msg.Envelope.ReplyWith(res);

				break;
			case FilteredReadAllResult.Error:
			case FilteredReadAllResult.AccessDenied:
			case FilteredReadAllResult.InvalidPosition:
				msg.Envelope.ReplyWith(res);
				break;
			default:
				throw new ArgumentOutOfRangeException($"Unknown ReadAllResult: {res.Result}");
		}
	}

	async ValueTask IAsyncHandle<StorageMessage.EffectiveStreamAclRequest>.HandleAsync(StorageMessage.EffectiveStreamAclRequest msg, CancellationToken token) {
		Message reply;
		var cts = token.LinkTo(msg.CancellationToken);

		try {
			var acl = await _readIndex.GetEffectiveAcl(_readIndex.GetStreamId(msg.StreamId), token);
			reply = new StorageMessage.EffectiveStreamAclResponse(acl);
		} catch (OperationCanceledException e) when (e.CausedBy(cts, msg.CancellationToken)) {
			reply = new StorageMessage.OperationCancelledMessage(msg.CancellationToken);
		} catch (OperationCanceledException ex) when (ex.CancellationToken == cts?.Token) {
			throw new OperationCanceledException(null, ex, cts.CancellationOrigin);
		} finally {
			cts?.Dispose();
		}

		msg.Envelope.ReplyWith(reply);
	}

	private async ValueTask<ReadEventCompleted> ReadEvent(ReadEvent msg, CancellationToken token) {
		try {
			var streamName = msg.EventStreamId;
			var streamId = _readIndex.GetStreamId(streamName);
			var result = await _readIndex.ReadEvent(streamName, streamId, msg.EventNumber, token);
			var record = result.Result is ReadEventResult.Success && msg.ResolveLinkTos
				? await ResolveLinkToEvent(result.Record, msg.User, null, token)
				: ResolvedEvent.ForUnresolvedEvent(result.Record);
			if (record is null)
				return NoData(msg, ReadEventResult.AccessDenied);
			if (result.Result is ReadEventResult.NoStream or ReadEventResult.NotFound &&
			    _systemStreams.IsMetaStream(streamId) &&
			    result.OriginalStreamExists.HasValue &&
			    result.OriginalStreamExists.Value) {
				return NoData(msg, ReadEventResult.Success);
			}

			return new ReadEventCompleted(msg.CorrelationId, msg.EventStreamId, result.Result,
				record.Value, result.Metadata, false, null);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadEvent request.");
			return NoData(msg, ReadEventResult.Error, exc.Message);
		}
	}

	private async ValueTask<ReadStreamEventsForwardCompleted> ReadStreamEventsForward(ReadStreamEventsForward msg, CancellationToken token) {
		var lastIndexPosition = _readIndex.LastIndexedPosition;
		try {
			if (msg.MaxCount > MaxPageSize) {
				throw new ArgumentException($"Read size too big, should be less than {MaxPageSize} items");
			}

			var streamName = msg.EventStreamId;
			var streamId = _readIndex.GetStreamId(msg.EventStreamId);
			if (msg.ValidationStreamVersion.HasValue &&
			    await _readIndex.GetStreamLastEventNumber(streamId, token) == msg.ValidationStreamVersion)
				return NoData(msg, ReadStreamResult.NotModified, lastIndexPosition,
					msg.ValidationStreamVersion.Value);

			var result =
				await _readIndex.ReadStreamEventsForward(streamName, streamId, msg.FromEventNumber, msg.MaxCount, token);
			CheckEventsOrder(msg, result);
			if (await ResolveLinkToEvents(result.Records, msg.ResolveLinkTos, msg.User, token) is not { } resolvedPairs)
				return NoData(msg, ReadStreamResult.AccessDenied, lastIndexPosition);

			return new ReadStreamEventsForwardCompleted(
				msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
				(ReadStreamResult)result.Result, resolvedPairs, result.Metadata, false, string.Empty,
				result.NextEventNumber, result.LastEventNumber, result.IsEndOfStream, lastIndexPosition);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadStreamEventsForward request.");
			return NoData(msg, ReadStreamResult.Error, lastIndexPosition, error: exc.Message);
		}
	}

	private async ValueTask<ReadStreamEventsBackwardCompleted> ReadStreamEventsBackward(ReadStreamEventsBackward msg, CancellationToken token) {
		var lastIndexedPosition = _readIndex.LastIndexedPosition;
		try {
			if (msg.MaxCount > MaxPageSize) {
				throw new ArgumentException($"Read size too big, should be less than {MaxPageSize} items");
			}

			var streamName = msg.EventStreamId;
			var streamId = _readIndex.GetStreamId(msg.EventStreamId);
			if (msg.ValidationStreamVersion.HasValue &&
			    await _readIndex.GetStreamLastEventNumber(streamId, token) == msg.ValidationStreamVersion)
				return NoData(msg, ReadStreamResult.NotModified, lastIndexedPosition,
					msg.ValidationStreamVersion.Value);


			var result = await _readIndex.ReadStreamEventsBackward(streamName, streamId, msg.FromEventNumber,
				msg.MaxCount, token);
			CheckEventsOrder(msg, result);
			if (await ResolveLinkToEvents(result.Records, msg.ResolveLinkTos, msg.User, token) is not { } resolvedPairs)
				return NoData(msg, ReadStreamResult.AccessDenied, lastIndexedPosition);

			return new ReadStreamEventsBackwardCompleted(
				msg.CorrelationId, msg.EventStreamId, result.FromEventNumber, result.MaxCount,
				(ReadStreamResult)result.Result, resolvedPairs, result.Metadata, false, string.Empty,
				result.NextEventNumber, result.LastEventNumber, result.IsEndOfStream, lastIndexedPosition);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadStreamEventsBackward request.");
			return NoData(msg, ReadStreamResult.Error, lastIndexedPosition, error: exc.Message);
		}
	}

	private async ValueTask<ReadAllEventsForwardCompleted> ReadAllEventsForward(ReadAllEventsForward msg, CancellationToken token) {
		var pos = new TFPos(msg.CommitPosition, msg.PreparePosition);
		var lastIndexedPosition = _readIndex.LastIndexedPosition;
		try {
			if (msg.MaxCount > MaxPageSize) {
				throw new ArgumentException($"Read size too big, should be less than {MaxPageSize} items");
			}

			if (pos == TFPos.HeadOfTf) {
				var checkpoint = _writerCheckpoint.Read();
				pos = new TFPos(checkpoint, checkpoint);
			}

			if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
				return NoData(msg, ReadAllResult.InvalidPosition, pos, lastIndexedPosition, "Invalid position.");
			if (msg.ValidationTfLastCommitPosition == lastIndexedPosition)
				return NoData(msg, ReadAllResult.NotModified, pos, lastIndexedPosition);

			var res = await _readIndex.ReadAllEventsForward(pos, msg.MaxCount, token);
			if (await ResolveReadAllResult(res.Records, msg.ResolveLinkTos, msg.User, token) is not { } resolved)
				return NoData(msg, ReadAllResult.AccessDenied, pos, lastIndexedPosition);

			var metadata = await _readIndex.GetStreamMetadata(_systemStreams.AllStream, token);
			return new ReadAllEventsForwardCompleted(
				msg.CorrelationId, ReadAllResult.Success, null, resolved, metadata, false, msg.MaxCount,
				res.CurrentPos, res.NextPos, res.PrevPos, lastIndexedPosition);
		} catch (Exception exc) when (exc is InvalidReadException or UnableToReadPastEndOfStreamException) {
			Log.Warning(exc, "Error during processing ReadAllEventsBackward request. The read appears to be at an invalid position.");
			return NoData(msg, ReadAllResult.InvalidPosition, pos, lastIndexedPosition, exc.Message);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadAllEventsForward request.");
			return NoData(msg, ReadAllResult.Error, pos, lastIndexedPosition, exc.Message);
		}
	}

	private async ValueTask<ReadAllEventsBackwardCompleted> ReadAllEventsBackward(ReadAllEventsBackward msg, CancellationToken token) {
		var pos = new TFPos(msg.CommitPosition, msg.PreparePosition);
		var lastIndexedPosition = _readIndex.LastIndexedPosition;
		try {
			if (msg.MaxCount > MaxPageSize) {
				throw new ArgumentException($"Read size too big, should be less than {MaxPageSize} items");
			}

			if (pos == TFPos.HeadOfTf) {
				var checkpoint = _writerCheckpoint.Read();
				pos = new TFPos(checkpoint, checkpoint);
			}

			if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
				return NoData(msg, ReadAllResult.InvalidPosition, pos, lastIndexedPosition, "Invalid position.");
			if (msg.ValidationTfLastCommitPosition == lastIndexedPosition)
				return NoData(msg, ReadAllResult.NotModified, pos, lastIndexedPosition);

			var res = await _readIndex.ReadAllEventsBackward(pos, msg.MaxCount, token);
			if (await ResolveReadAllResult(res.Records, msg.ResolveLinkTos, msg.User, token) is not { } resolved)
				return NoData(msg, ReadAllResult.AccessDenied, pos, lastIndexedPosition);

			var metadata = await _readIndex.GetStreamMetadata(_systemStreams.AllStream, token);
			return new ReadAllEventsBackwardCompleted(
				msg.CorrelationId, ReadAllResult.Success, null, resolved, metadata, false, msg.MaxCount,
				res.CurrentPos, res.NextPos, res.PrevPos, lastIndexedPosition);
		} catch (Exception exc) when (exc is InvalidReadException or UnableToReadPastEndOfStreamException) {
			Log.Warning(exc, "Error during processing ReadAllEventsBackward request. The read appears to be at an invalid position.");
			return NoData(msg, ReadAllResult.InvalidPosition, pos, lastIndexedPosition, exc.Message);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadAllEventsBackward request.");
			return NoData(msg, ReadAllResult.Error, pos, lastIndexedPosition, exc.Message);
		}
	}

	private async ValueTask<FilteredReadAllEventsForwardCompleted> FilteredReadAllEventsForward(FilteredReadAllEventsForward msg, CancellationToken token) {
		var pos = new TFPos(msg.CommitPosition, msg.PreparePosition);
		var lastIndexedPosition = _readIndex.LastIndexedPosition;
		try {
			if (msg.MaxCount > MaxPageSize) {
				throw new ArgumentException($"Read size too big, should be less than {MaxPageSize} items");
			}

			if (pos == TFPos.HeadOfTf) {
				var checkpoint = _writerCheckpoint.Read();
				pos = new TFPos(checkpoint, checkpoint);
			}

			if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
				return NoDataForFilteredCommand(msg, FilteredReadAllResult.InvalidPosition, pos, lastIndexedPosition,
					"Invalid position.");
			if (msg.ValidationTfLastCommitPosition == lastIndexedPosition)
				return NoDataForFilteredCommand(msg, FilteredReadAllResult.NotModified, pos,
					lastIndexedPosition);

			var res = await _readIndex.ReadAllEventsForwardFiltered(pos, msg.MaxCount, msg.MaxSearchWindow,
				msg.EventFilter, token);
			if (await ResolveReadAllResult(res.Records, msg.ResolveLinkTos, msg.User, token) is not { } resolved)
				return NoDataForFilteredCommand(msg, FilteredReadAllResult.AccessDenied, pos,
					lastIndexedPosition);

			var metadata = await _readIndex.GetStreamMetadata(_systemStreams.AllStream, token);
			return new FilteredReadAllEventsForwardCompleted(
				msg.CorrelationId, FilteredReadAllResult.Success, null, resolved, metadata, false,
				msg.MaxCount,
				res.CurrentPos, res.NextPos, res.PrevPos, lastIndexedPosition, res.IsEndOfStream,
				res.ConsideredEventsCount);
		} catch (Exception exc) when (exc is InvalidReadException or UnableToReadPastEndOfStreamException) {
			Log.Warning(exc, "Error during processing ReadAllEventsForwardFiltered request. The read appears to be at an invalid position.");
			return NoDataForFilteredCommand(msg, FilteredReadAllResult.InvalidPosition, pos, lastIndexedPosition, exc.Message);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadAllEventsForwardFiltered request.");
			return NoDataForFilteredCommand(msg, FilteredReadAllResult.Error, pos, lastIndexedPosition,
				exc.Message);
		}
	}

	private async ValueTask<FilteredReadAllEventsBackwardCompleted> FilteredReadAllEventsBackward(FilteredReadAllEventsBackward msg, CancellationToken token) {
		var pos = new TFPos(msg.CommitPosition, msg.PreparePosition);
		var lastIndexedPosition = _readIndex.LastIndexedPosition;
		try {
			if (msg.MaxCount > MaxPageSize) {
				throw new ArgumentException($"Read size too big, should be less than {MaxPageSize} items");
			}

			if (pos == TFPos.HeadOfTf) {
				var checkpoint = _writerCheckpoint.Read();
				pos = new TFPos(checkpoint, checkpoint);
			}

			if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
				return NoDataForFilteredCommand(msg, FilteredReadAllResult.InvalidPosition, pos, lastIndexedPosition,
					"Invalid position.");
			if (msg.ValidationTfLastCommitPosition == lastIndexedPosition)
				return NoDataForFilteredCommand(msg, FilteredReadAllResult.NotModified, pos,
					lastIndexedPosition);

			var res = await _readIndex.ReadAllEventsBackwardFiltered(pos, msg.MaxCount, msg.MaxSearchWindow,
				msg.EventFilter, token);
			if (await ResolveReadAllResult(res.Records, msg.ResolveLinkTos, msg.User, token) is not { } resolved)
				return NoDataForFilteredCommand(msg, FilteredReadAllResult.AccessDenied, pos,
					lastIndexedPosition);

			var metadata = await _readIndex.GetStreamMetadata(_systemStreams.AllStream, token);
			return new FilteredReadAllEventsBackwardCompleted(
				msg.CorrelationId, FilteredReadAllResult.Success, null, resolved, metadata, false,
				msg.MaxCount,
				res.CurrentPos, res.NextPos, res.PrevPos, lastIndexedPosition, res.IsEndOfStream);
		} catch (Exception exc) when (exc is InvalidReadException or UnableToReadPastEndOfStreamException) {
			Log.Warning(exc, "Error during processing ReadAllEventsBackwardFiltered request. The read appears to be at an invalid position.");
			return NoDataForFilteredCommand(msg, FilteredReadAllResult.InvalidPosition, pos, lastIndexedPosition, exc.Message);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error during processing ReadAllEventsBackwardFiltered request.");
			return NoDataForFilteredCommand(msg, FilteredReadAllResult.Error, pos, lastIndexedPosition,
				exc.Message);
		}
	}

	private static ReadEventCompleted NoData(ReadEvent msg, ReadEventResult result, string error = null) {
		return new(msg.CorrelationId, msg.EventStreamId, result, ResolvedEvent.EmptyEvent, null, false, error);
	}

	private static ReadStreamEventsForwardCompleted NoData(ReadStreamEventsForward msg,
		ReadStreamResult result, long lastIndexedPosition, long lastEventNumber = -1, string error = null) {
		return new(
			msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, result,
			EmptyRecords, null, false, error ?? string.Empty, -1, lastEventNumber, true, lastIndexedPosition);
	}

	private static ReadStreamEventsBackwardCompleted NoData(ReadStreamEventsBackward msg, ReadStreamResult result, long lastIndexedPosition,
		long lastEventNumber = -1, string error = null) {
		return new(
			msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, result,
			EmptyRecords, null, false, error ?? string.Empty, -1, lastEventNumber, true, lastIndexedPosition);
	}

	private static ReadAllEventsForwardCompleted NoData(ReadAllEventsForward msg,
		ReadAllResult result, TFPos pos, long lastIndexedPosition, string error = null) {
		return new(
			msg.CorrelationId, result, error, ResolvedEvent.EmptyArray, null, false,
			msg.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, lastIndexedPosition);
	}

	private static FilteredReadAllEventsForwardCompleted NoDataForFilteredCommand(
		FilteredReadAllEventsForward msg, FilteredReadAllResult result, TFPos pos,
		long lastIndexedPosition, string error = null) {
		return new(
			msg.CorrelationId, result, error, ResolvedEvent.EmptyArray, null, false,
			msg.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, lastIndexedPosition, false, 0L);
	}

	private static FilteredReadAllEventsBackwardCompleted NoDataForFilteredCommand(
		FilteredReadAllEventsBackward msg, FilteredReadAllResult result, TFPos pos,
		long lastIndexedPosition, string error = null) {
		return new(
			msg.CorrelationId, result, error, ResolvedEvent.EmptyArray, null, false,
			msg.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, lastIndexedPosition, false);
	}

	private static ReadAllEventsBackwardCompleted NoData(ReadAllEventsBackward msg,
		ReadAllResult result, TFPos pos, long lastIndexedPosition, string error = null) {
		return new(
			msg.CorrelationId, result, error, ResolvedEvent.EmptyArray, null, false,
			msg.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, lastIndexedPosition);
	}

	private static void CheckEventsOrder(ReadStreamEventsForward msg, IndexReadStreamResult result) {
		for (var index = 1; index < result.Records.Length; index++) {
			if (result.Records[index].EventNumber != result.Records[index - 1].EventNumber + 1) {
				throw new Exception(
					$"Invalid order of events has been detected in read index for the event stream '{msg.EventStreamId}'. " +
					$"The event {result.Records[index].EventNumber} at position {result.Records[index].LogPosition} goes after the event {result.Records[index - 1].EventNumber} at position {result.Records[index - 1].LogPosition}");
			}
		}
	}

	private static void CheckEventsOrder(ReadStreamEventsBackward msg, IndexReadStreamResult result) {
		for (var index = 1; index < result.Records.Length; index++) {
			if (result.Records[index].EventNumber != result.Records[index - 1].EventNumber - 1) {
				throw new Exception(
					$"Invalid order of events has been detected in read index for the event stream '{msg.EventStreamId}'. " +
					$"The event {result.Records[index].EventNumber} at position {result.Records[index].LogPosition} goes after the event {result.Records[index - 1].EventNumber} at position {result.Records[index - 1].LogPosition}");
			}
		}
	}

	private async ValueTask<IReadOnlyList<ResolvedEvent>> ResolveLinkToEvents(IReadOnlyList<EventRecord> records, bool resolveLinks, ClaimsPrincipal user, CancellationToken token) {
		var resolved = new ResolvedEvent[records.Count];
		if (resolveLinks) {
			for (var i = 0; i < records.Count; i++) {
				if (await ResolveLinkToEvent(records[i], user, null, token) is not { } rec)
					return null;

				resolved[i] = rec;
			}
		} else {
			for (int i = 0; i < records.Count; ++i) {
				resolved[i] = ResolvedEvent.ForUnresolvedEvent(records[i]);
			}
		}

		return resolved;
	}

	private async ValueTask<ResolvedEvent?> ResolveLinkToEvent(EventRecord eventRecord, ClaimsPrincipal user, long? commitPosition, CancellationToken token) {
		if (eventRecord.EventType is not SystemEventTypes.LinkTo) {
			return ResolvedEvent.ForUnresolvedEvent(eventRecord, commitPosition);
		}

		try {
			var linkPayload = Helper.UTF8NoBom.GetString(eventRecord.Data.Span);
			var parts = linkPayload.Split(LinkToSeparator, 2);
			if (long.TryParse(parts[0], out long eventNumber)) {
				var streamName = parts[1];
				var streamId = _readIndex.GetStreamId(streamName);
				var res = await _readIndex.ReadEvent(streamName, streamId, eventNumber, token);
				return res.Result is ReadEventResult.Success
					? ResolvedEvent.ForResolvedLink(res.Record, eventRecord, commitPosition)
					: ResolvedEvent.ForFailedResolvedLink(eventRecord, res.Result, commitPosition);
			}

			Log.Warning($"Invalid link event payload [{linkPayload}]: {eventRecord}");
			return ResolvedEvent.ForUnresolvedEvent(eventRecord, commitPosition);
		} catch (Exception exc) when (exc is not OperationCanceledException oce || oce.CancellationToken != token) {
			Log.Error(exc, "Error while resolving link for event record: {eventRecord}", eventRecord.ToString());
		}

		// return unresolved link
		return ResolvedEvent.ForFailedResolvedLink(eventRecord, ReadEventResult.Error, commitPosition);
	}

	private async ValueTask<IReadOnlyList<ResolvedEvent>> ResolveReadAllResult(List<CommitEventRecord> records, bool resolveLinks,
		ClaimsPrincipal user, CancellationToken token) {
		var result = new ResolvedEvent[records.Count];
		if (resolveLinks) {
			for (var i = 0; i < result.Length; ++i) {
				var record = records[i];
				if (await ResolveLinkToEvent(record.Event, user, record.CommitPosition, token) is not { } resolvedPair)
					return null;
				result[i] = resolvedPair;
			}
		} else {
			for (var i = 0; i < result.Length; ++i) {
				result[i] = ResolvedEvent.ForUnresolvedEvent(records[i].Event, records[i].CommitPosition);
			}
		}

		return result;
	}

	public void Handle(StorageMessage.BatchLogExpiredMessages message) {
		if (!_batchLoggingEnabled)
			return;
		if (_expiredBatchCount == 0) {
			_batchLoggingEnabled = false;
			Log.Warning("StorageReaderWorker #{0}: Batch logging disabled, read load is back to normal", queueId);
			return;
		}

		Log.Warning("StorageReaderWorker #{0}: {1} read operations have expired", queueId, _expiredBatchCount);
		_expiredBatchCount = 0;
		publisher.Publish(
			TimerMessage.Schedule.Create(TimeSpan.FromSeconds(2),
				publisher,
				new StorageMessage.BatchLogExpiredMessages(Guid.NewGuid(), queueId))
		);
	}

	private bool LogExpiredMessage(DateTime expire) {
		if (!_lastExpireTime.HasValue) {
			_expiredBatchCount = 1;
			_lastExpireTime = expire;
			return true;
		}

		if (_batchLoggingEnabled) {
			_expiredBatchCount++;
			_lastExpireTime = expire;
			return false;
		}

		_expiredBatchCount++;
		if (_expiredBatchCount < 50) return true;

		if (expire - _lastExpireTime.Value > TimeSpan.FromSeconds(1)) {
			_expiredBatchCount = 1;
			_lastExpireTime = expire;

			return true;
		}

		//heuristic to match approximately >= 50 expired messages / second
		_batchLoggingEnabled = true;
		Log.Warning("StorageReaderWorker #{0}: Batch logging enabled, high rate of expired read messages detected", queueId);
		publisher.Publish(
			TimerMessage.Schedule.Create(TimeSpan.FromSeconds(2),
				publisher,
				new StorageMessage.BatchLogExpiredMessages(Guid.NewGuid(), queueId))
		);
		_expiredBatchCount = 1;
		_lastExpireTime = expire;
		return false;
	}

	async ValueTask IAsyncHandle<StorageMessage.StreamIdFromTransactionIdRequest>.HandleAsync(StorageMessage.StreamIdFromTransactionIdRequest message, CancellationToken token) {
		var cts = token.LinkTo(message.CancellationToken);
		Message reply;
		try {
			var streamId = await _readIndex.GetEventStreamIdByTransactionId(message.TransactionId, token);
			var streamName = await _readIndex.GetStreamName(streamId, token);
			reply = new StorageMessage.StreamIdFromTransactionIdResponse(streamName);
		} catch (OperationCanceledException ex) when (ex.CausedBy(cts, message.CancellationToken)) {
			reply = new StorageMessage.OperationCancelledMessage(message.CancellationToken);
		} catch (OperationCanceledException ex) when (ex.CancellationToken == cts?.Token) {
			throw new OperationCanceledException(null, ex, cts.CancellationOrigin);
		} finally {
			cts?.Dispose();
		}

		message.Envelope.ReplyWith(reply);
	}
}
