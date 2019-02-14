using System;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Builds a <see cref="PersistentSubscriptionSettings"/> object.
	/// </summary>
	public class PersistentSubscriptionSettingsBuilder {
		private bool _resolveLinkTos;
		private long _startFrom;
		private bool _timingStatistics;
		private TimeSpan _timeout;
		private int _readBatchSize;
		private int _maxRetryCount;
		private int _liveBufferSize;
		private int _bufferSize;
		private TimeSpan _checkPointAfter;
		private int _minCheckPointCount;
		private int _maxCheckPointCount;
		private int _maxSubscriberCount;
		private string _namedConsumerStrategies;

		internal PersistentSubscriptionSettingsBuilder(bool resolveLinkTos, long startFrom, bool timingStatistics,
			TimeSpan timeout,
			int bufferSize, int liveBufferSize, int maxRetryCount, int readBatchSize,
			TimeSpan checkPointAfter, int minCheckPointCount,
			int maxCheckPointCount, int maxSubscriberCount, string namedConsumerStrategies) {
			_resolveLinkTos = resolveLinkTos;
			_startFrom = startFrom;
			_timingStatistics = timingStatistics;
			_timeout = timeout;
			_bufferSize = bufferSize;
			_liveBufferSize = liveBufferSize;
			_maxRetryCount = maxRetryCount;
			_readBatchSize = readBatchSize;
			_checkPointAfter = checkPointAfter;
			_minCheckPointCount = minCheckPointCount;
			_maxCheckPointCount = maxCheckPointCount;
			_maxSubscriberCount = maxSubscriberCount;
			_namedConsumerStrategies = namedConsumerStrategies;
		}


		/// <summary>
		/// Sets the option to include further latency statistics. These statistics have a cost and should not be used
		/// in high performance situations.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithExtraStatistics() {
			_timingStatistics = true;
			return this;
		}

		/// <summary>
		/// Sets the option to resolve linktos on events that are found for this subscription.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder ResolveLinkTos() {
			_resolveLinkTos = true;
			return this;
		}

		/// <summary>
		/// Sets the option to not resolve linktos on events that are found for this subscription.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder DoNotResolveLinkTos() {
			_resolveLinkTos = false;
			return this;
		}

		/// <summary>
		/// If set the subscription will prefer if possible to round robin between the clients that
		/// are connected.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder PreferRoundRobin() {
			_namedConsumerStrategies = SystemConsumerStrategies.RoundRobin;
			return this;
		}

		/// <summary>
		/// If set the subscription will prefer if possible to dispatch only to a single of the connected
		/// clients. If however the buffer limits are reached on that client it will begin sending to other 
		/// clients.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder PreferDispatchToSingle() {
			_namedConsumerStrategies = SystemConsumerStrategies.DispatchToSingle;
			return this;
		}

		/// <summary>
		/// Sets that the subscription should start from the beginning of the stream.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder StartFromBeginning() {
			_startFrom = 0;
			return this;
		}

		/// <summary>
		/// Sets that the subscription should start from a specified location of the stream.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder StartFrom(long position) {
			_startFrom = position;
			return this;
		}

		/// <summary>
		/// Sets the timeout for a message (will be retried if an ack is not received within this timespan)
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithMessageTimeoutOf(TimeSpan timeout) {
			_timeout = timeout;
			return this;
		}

		/// <summary>
		/// Sets the timeout timespan to about 30k years. If you need larger please let us know.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder DontTimeoutMessages() {
			_timeout = TimeSpan.Zero;
			return this;
		}


		/// <summary>
		/// Sets that the backend should try to checkpoint the subscription after some
		/// period of time. Note that if the increment of the checkpoint would be below
		/// the minimum the stream will not be checkpointed at this time.
		/// </summary>
		/// <remarks>
		/// It is important to tweak checkpointing for high performance streams as they cause 
		/// writes to happen back in the system. There is a trade off between the number of
		/// writes that can happen in varying failure scenarios and the frequency of 
		/// writing out the checkpoints within the system. Normally settings such
		/// as once per second with a minimum of 5-10 messages and a high max to checkpoint should
		/// be a good compromise for most streams though you may want to change this if you
		/// for instance are doing hundreds of messages/second through the subscription.
		/// </remarks>
		/// <param name="time">The amount of time to try checkpointing after</param>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder CheckPointAfter(TimeSpan time) {
			_checkPointAfter = time;
			return this;
		}

		/// <summary>
		/// Sets the minimum checkpoint count. The subscription will not increment a checkpoint
		/// below this value eg if there is one item to checkpoint and it is set to five it
		/// will not checkpoint
		/// </summary>
		/// <remarks>
		/// It is important to tweak checkpointing for high performance streams as they cause 
		/// writes to happen back in the system. There is a trade off between the number of
		/// writes that can happen in varying failure scenarios and the frequency of 
		/// writing out the checkpoints within the system. Normally settings such
		/// as once per second with a minimum of 5-10 messages and a high max to checkpoint should
		/// be a good compromise for most streams though you may want to change this if you
		/// for instance are doing hundreds of messages/second through the subscription.
		/// </remarks>
		/// <param name="count">The minimum count to checkpoint</param>
		/// <returns></returns>
		public PersistentSubscriptionSettingsBuilder MinimumCheckPointCountOf(int count) {
			_minCheckPointCount = count;
			return this;
		}

		/// <summary>
		/// Sets the largest increment the subscription will checkpoint. If this value is 
		/// reached the subscription will immediately write a checkpoint. As such this value
		/// should normally be reasonably large so as not to cause too many writes to occur in 
		/// the subscription
		/// </summary>
		/// <remarks>
		/// It is important to tweak checkpointing for high performance streams as they cause 
		/// writes to happen back in the system. There is a trade off between the number of
		/// writes that can happen in varying failure scenarios and the frequency of 
		/// writing out the checkpoints within the system. Normally settings such
		/// as once per second with a minimum of 5-10 messages and a high max to checkpoint should
		/// be a good compromise for most streams though you may want to change this if you
		/// for instance are doing hundreds of messages/second through the subscription.
		/// </remarks>
		/// <param name="count">The maximum count to checkpoint</param>
		/// <returns></returns>
		public PersistentSubscriptionSettingsBuilder MaximumCheckPointCountOf(int count) {
			_maxCheckPointCount = count;
			return this;
		}


		/// <summary>
		/// Sets the number of times a message should be retried before being considered a bad message
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithMaxRetriesOf(int count) {
			Ensure.Nonnegative(count, "count");
			_maxRetryCount = count;
			return this;
		}

		/// <summary>
		/// Sets the size of the live buffer for the subscription. This is the buffer used 
		/// to cache messages while sending messages as they happen. The count is
		/// in terms of the number of messages to cache.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithLiveBufferSizeOf(int count) {
			Ensure.Nonnegative(count, "count");
			_liveBufferSize = count;
			return this;
		}


		/// <summary>
		/// Sets the size of the read batch used when paging in history for the subscription
		/// sizes should not be too big ...
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithReadBatchOf(int count) {
			Ensure.Nonnegative(count, "count");
			_readBatchSize = count;
			return this;
		}


		/// <summary>
		/// Sets the size of the read batch used when paging in history for the subscription
		/// sizes should not be too big ...
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithBufferSizeOf(int count) {
			Ensure.Nonnegative(count, "count");
			_bufferSize = count;
			return this;
		}

		/// <summary>
		/// Sets that the subscription should start from where the stream is when the subscription is first connected.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder StartFromCurrent() {
			_startFrom = -1;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of subscribers allowed to connect.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithMaxSubscriberCountOf(int count) {
			Ensure.Nonnegative(count, "count");
			_maxSubscriberCount = count;
			return this;
		}

		/// <summary>
		/// Sets the consumer strategy for distributing event to clients. See <see cref="SystemConsumerStrategies"/> for system supported strategies.
		/// </summary>
		/// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
		public PersistentSubscriptionSettingsBuilder WithNamedConsumerStrategy(string namedConsumerStrategy) {
			Ensure.NotNullOrEmpty(namedConsumerStrategy, "namedConsumerStrategy");
			_namedConsumerStrategies = namedConsumerStrategy;
			return this;
		}


		/// <summary>
		/// Builds a <see cref="PersistentSubscriptionSettings"/> object from a <see cref="PersistentSubscriptionSettingsBuilder"/>.
		/// </summary>
		/// <param name="builder"><see cref="PersistentSubscriptionSettingsBuilder"/> from which to build a <see cref="PersistentSubscriptionSettingsBuilder"/></param>
		/// <returns></returns>
		public static implicit operator PersistentSubscriptionSettings(PersistentSubscriptionSettingsBuilder builder) {
			return builder.Build();
		}

		/// <summary>
		/// Builds a <see cref="PersistentSubscriptionSettings"/> object from a <see cref="PersistentSubscriptionSettingsBuilder"/>.
		/// </summary>
		///         /// <returns></returns>
		public PersistentSubscriptionSettings Build() {
			return new PersistentSubscriptionSettings(_resolveLinkTos,
				_startFrom,
				_timingStatistics,
				_timeout,
				_maxRetryCount,
				_liveBufferSize,
				_readBatchSize,
				_bufferSize,
				_checkPointAfter,
				_minCheckPointCount,
				_maxCheckPointCount,
				_maxSubscriberCount,
				_namedConsumerStrategies);
		}
	}
}
