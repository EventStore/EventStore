using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class UpdatePersistentSubscriptionOperation : OperationBase<PersistentSubscriptionUpdateResult,
		ClientMessage.UpdatePersistentSubscriptionCompleted> {
		private readonly string _stream;
		private readonly string _groupName;
		private readonly bool _resolveLinkTos;
		private readonly long _startFromBeginning;
		private readonly int _messageTimeoutMilliseconds;
		private readonly bool _recordStatistics;
		private readonly int _maxRetryCount;
		private readonly int _liveBufferSize;
		private readonly int _readBatchSize;
		private readonly int _bufferSize;
		private readonly int _checkPointAfter;
		private readonly int _minCheckPointCount;
		private readonly int _maxCheckPointCount;
		private readonly string _namedConsumerStrategy;
		private readonly int _maxSubscriberCount;

		public UpdatePersistentSubscriptionOperation(ILogger log,
			TaskCompletionSource<PersistentSubscriptionUpdateResult> source,
			string stream,
			string groupName,
			PersistentSubscriptionSettings settings,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.UpdatePersistentSubscription,
				TcpCommand.UpdatePersistentSubscriptionCompleted, userCredentials) {
			Ensure.NotNull(settings, "settings");
			_resolveLinkTos = settings.ResolveLinkTos;
			_stream = stream;
			_groupName = groupName;
			_startFromBeginning = settings.StartFrom;
			_maxRetryCount = settings.MaxRetryCount;
			_liveBufferSize = settings.LiveBufferSize;
			_readBatchSize = settings.ReadBatchSize;
			_bufferSize = settings.HistoryBufferSize;
			_recordStatistics = settings.ExtraStatistics;
			_messageTimeoutMilliseconds = (int)settings.MessageTimeout.TotalMilliseconds;
			_checkPointAfter = (int)settings.CheckPointAfter.TotalMilliseconds;
			_minCheckPointCount = settings.MinCheckPointCount;
			_maxCheckPointCount = settings.MaxCheckPointCount;
			_maxSubscriberCount = settings.MaxSubscriberCount;
			_namedConsumerStrategy = settings.NamedConsumerStrategy;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.UpdatePersistentSubscription(_groupName, _stream, _resolveLinkTos,
				_startFromBeginning, _messageTimeoutMilliseconds,
				_recordStatistics, _liveBufferSize, _readBatchSize, _bufferSize, _maxRetryCount,
				_namedConsumerStrategy == SystemConsumerStrategies.RoundRobin, _checkPointAfter,
				_maxCheckPointCount, _minCheckPointCount, _maxSubscriberCount, _namedConsumerStrategy);
		}

		protected override InspectionResult InspectResponse(
			ClientMessage.UpdatePersistentSubscriptionCompleted response) {
			switch (response.Result) {
				case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Fail:
					Fail(new InvalidOperationException(String.Format(
						"Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream, response.Reason)));
					return new InspectionResult(InspectionDecision.EndOperation, "Fail");
				case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult
					.AccessDenied:
					Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult
					.DoesNotExist:
					Fail(new InvalidOperationException(
						String.Format("Subscription group {0} on stream {1} does not exist", _groupName, _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "DoesNotExist");
				default:
					throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
			}
		}

		protected override PersistentSubscriptionUpdateResult TransformResponse(
			ClientMessage.UpdatePersistentSubscriptionCompleted response) {
			return new PersistentSubscriptionUpdateResult(PersistentSubscriptionUpdateStatus.Success);
		}

		public override string ToString() {
			return string.Format("Stream: {0}, Group Name: {1}", _stream, _groupName);
		}
	}
}
