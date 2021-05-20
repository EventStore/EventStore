using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using Serilog;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionStreamReader : IPersistentSubscriptionStreamReader {
		private static readonly ILogger Log = Serilog.Log.ForContext<PersistentSubscriptionStreamReader>();

		public const int MaxPullBatchSize = 500;
		private const int MaxRetryTimeSecs = 60;
		private const int MaxExponentialBackoffPower = 10; /*to prevent integer overflow*/

		private readonly IODispatcher _ioDispatcher;
		private readonly int _maxPullBatchSize;
		private readonly Random _random = new Random();

		public PersistentSubscriptionStreamReader(IODispatcher ioDispatcher, int maxPullBatchSize) {
			_ioDispatcher = ioDispatcher;
			_maxPullBatchSize = maxPullBatchSize;
		}

		public void BeginReadEvents(IPersistentSubscriptionEventSource eventSource,
			IPersistentSubscriptionStreamPosition startPosition, int countToLoad, int batchSize, bool resolveLinkTos,
			bool skipFirstEvent,
			Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onEventsFound, Action<string> onError) {
			BeginReadEventsInternal(eventSource, startPosition, countToLoad, batchSize, resolveLinkTos,
				skipFirstEvent, onEventsFound, onError, 0);
		}

		private int GetBackOffDelay(int retryCount) {
			//exponential backoff + jitter
			return 1 + _random.Next(0, 1 + Math.Min(MaxRetryTimeSecs, (1 << Math.Min(retryCount, MaxExponentialBackoffPower))));
		}

		private void BeginReadEventsInternal(IPersistentSubscriptionEventSource eventSource,
			IPersistentSubscriptionStreamPosition startPosition, int countToLoad, int batchSize, bool resolveLinkTos, bool skipFirstEvent,
			Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onEventsFound, Action<string> onError, int retryCount) {
			var actualBatchSize = GetBatchSize(batchSize);

			if (eventSource.FromStream) {
				_ioDispatcher.ReadForward(
					eventSource.EventStreamId, startPosition.StreamEventNumber, Math.Min(countToLoad, actualBatchSize),
					resolveLinkTos, SystemAccounts.System, new ResponseHandler(onEventsFound, onError, skipFirstEvent).FetchCompleted,
					async () => {
						var backOff = GetBackOffDelay(retryCount);
						Log.Warning("Timed out reading from stream: {stream}. Retrying in {retryInterval} seconds.", eventSource.EventStreamId, backOff);
						await Task.Delay(TimeSpan.FromSeconds(backOff)).ConfigureAwait(false);
						BeginReadEventsInternal(eventSource, startPosition, countToLoad, batchSize, resolveLinkTos,
						skipFirstEvent, onEventsFound, onError, retryCount + 1);
					}, Guid.NewGuid());

			} else if (eventSource.FromAll) {
				_ioDispatcher.ReadAllForward(
					startPosition.TFPosition.Commit,
					startPosition.TFPosition.Prepare,
					Math.Min(countToLoad, actualBatchSize),
					resolveLinkTos,
					true,
					null,
					SystemAccounts.System,
					null,
					new ResponseHandler(onEventsFound, onError, skipFirstEvent).FetchAllCompleted,
					async () => {
						var backOff = GetBackOffDelay(retryCount);
						Log.Warning("Timed out reading from stream: {stream}. Retrying in {retryInterval} seconds.", SystemStreams.AllStream, backOff);
						await Task.Delay(TimeSpan.FromSeconds(backOff)).ConfigureAwait(false);
						BeginReadEventsInternal(eventSource, startPosition, countToLoad, batchSize, resolveLinkTos,
							skipFirstEvent, onEventsFound, onError, retryCount + 1);
					}, Guid.NewGuid());
			} else {
				throw new InvalidOperationException();
			}
		}

		private int GetBatchSize(int batchSize) {
			return Math.Min(Math.Min(batchSize == 0 ? 20 : batchSize, MaxPullBatchSize), _maxPullBatchSize);
		}

		private class ResponseHandler {
			private readonly Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> _onFetchCompleted;
			private readonly Action<string> _onError;
			private readonly bool _skipFirstEvent;

			public ResponseHandler(Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onFetchCompleted, Action<string> onError, bool skipFirstEvent) {
				_onFetchCompleted = onFetchCompleted;
				_skipFirstEvent = skipFirstEvent;
				_onError = onError;
			}

			public void FetchCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg) {
				switch (msg.Result) {
					case ReadStreamResult.Success:
					case ReadStreamResult.NoStream:
						_onFetchCompleted(_skipFirstEvent ? msg.Events.Skip(1).ToArray() : msg.Events, new PersistentSubscriptionSingleStreamPosition(msg.NextEventNumber), msg.IsEndOfStream);
						break;
					case ReadStreamResult.AccessDenied:
						_onError($"Read access denied for stream: {msg.EventStreamId}");
						break;
					default:
						_onError(msg.Error ?? $"Error reading stream: {msg.EventStreamId} at event number: {msg.FromEventNumber}");
						break;
				}
			}

			public void FetchAllCompleted(ClientMessage.ReadAllEventsForwardCompleted msg) {
				switch (msg.Result) {
					case ReadAllResult.Success:
						_onFetchCompleted(_skipFirstEvent ? msg.Events.Skip(1).ToArray() : msg.Events, new PersistentSubscriptionAllStreamPosition(msg.NextPos.CommitPosition, msg.NextPos.PreparePosition), msg.IsEndOfStream);
						break;
					case ReadAllResult.AccessDenied:
						_onError($"Read access denied for stream: {SystemStreams.AllStream}");
						break;
					default:
						_onError(msg.Error ?? $"Error reading stream: {SystemStreams.AllStream} at position: {msg.CurrentPos}");
						break;
				}
			}
		}
	}
}
