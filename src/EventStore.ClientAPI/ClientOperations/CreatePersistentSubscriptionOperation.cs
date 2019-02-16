using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class CreatePersistentSubscriptionOperation : OperationBase<PersistentSubscriptionCreateResult,
		ClientMessage.CreatePersistentSubscriptionCompleted> {
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
		private readonly int _maxSubscriberCount;
		private readonly string _namedConsumerStrategy;

		public CreatePersistentSubscriptionOperation(ILogger log,
			TaskCompletionSource<PersistentSubscriptionCreateResult> source,
			string stream,
			string groupName,
			PersistentSubscriptionSettings settings,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.CreatePersistentSubscription,
				TcpCommand.CreatePersistentSubscriptionCompleted, userCredentials) {
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
			return new ClientMessage.CreatePersistentSubscription(_groupName, _stream, _resolveLinkTos,
				_startFromBeginning, _messageTimeoutMilliseconds,
				_recordStatistics, _liveBufferSize, _readBatchSize, _bufferSize, _maxRetryCount,
				_namedConsumerStrategy == SystemConsumerStrategies.RoundRobin, _checkPointAfter,
				_maxCheckPointCount, _minCheckPointCount, _maxSubscriberCount, _namedConsumerStrategy);
		}

		protected override InspectionResult InspectResponse(
			ClientMessage.CreatePersistentSubscriptionCompleted response) {
			switch (response.Result) {
				case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Fail:
					Fail(new InvalidOperationException(String.Format(
						"Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream, response.Reason)));
					return new InspectionResult(InspectionDecision.EndOperation, "Fail");
				case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult
					.AccessDenied:
					Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult
					.AlreadyExists:
					Fail(new InvalidOperationException(
						String.Format("Subscription group {0} on stream {1} already exists", _groupName, _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "AlreadyExists");
				default:
					throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
			}
		}

		protected override PersistentSubscriptionCreateResult TransformResponse(
			ClientMessage.CreatePersistentSubscriptionCompleted response) {
			return new PersistentSubscriptionCreateResult(PersistentSubscriptionCreateStatus.Success);
		}

		public override string ToString() {
			return string.Format("Stream: {0}, Group Name: {1}", _stream, _groupName);
		}
	}
}
