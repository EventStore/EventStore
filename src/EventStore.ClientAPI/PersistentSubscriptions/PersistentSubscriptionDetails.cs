using System.Collections.Generic;

namespace EventStore.ClientAPI.PersistentSubscriptions {
	/// <summary>
	/// Details for a Persistent Subscription.
	/// </summary>
	public sealed class PersistentSubscriptionDetails {
		/// <summary>
		/// Configuration of persistent subscription.
		/// </summary>
		/// <remarks>Only populated when retrieved via <see cref="PersistentSubscriptionsManager.Describe"/> method.</remarks>
		public PersistentSubscriptionConfigDetails Config { get; set; }

		/// <summary>
		/// List of current connections on this persistent subscription .
		/// </summary>
		/// <remarks>Only populated when retrieved via <see cref="PersistentSubscriptionsManager.Describe"/> method.</remarks>
		public List<PersistentSubscriptionConnectionDetails> Connections { get; set; }

		/// <summary>
		/// Target stream that refers to this subscription.
		/// </summary>
		public string EventStreamId { get; set; }

		/// <summary>
		/// The persistent subscription name.
		/// </summary>
		public string GroupName { get; set; }

		/// <summary>
		/// Current status.
		/// </summary>
		public string Status { get; set; }

		/// <summary>
		/// Average items per second (count).
		/// </summary>
		public decimal AverageItemsPerSecond { get; set; }

		/// <summary>
		/// Total items processed (count).
		/// </summary>
		public long TotalItemsProcessed { get; set; }

		/// <summary>
		/// Number of items seen since last measurement on this connection (used as the basis for <see cref="AverageItemsPerSecond"/>).
		/// </summary>
		public long CountSinceLastMeasurement { get; set; }

		/// <summary>
		/// Last processed target stream version.
		/// </summary>
		public long LastProcessedEventNumber { get; set; }

		/// <summary>
		/// Last checkpointed target stream version.
		/// </summary>
		public long LastKnownEventNumber { get; set; }

		/// <summary>
		/// Read buffer count.
		/// </summary>
		public int ReadBufferCount { get; set; }

		/// <summary>
		/// Live buffer count.
		/// </summary>
		public long LiveBufferCount { get; set; }

		/// <summary>
		/// Retry buffer count.
		/// </summary>
		public int RetryBufferCount { get; set; }

		/// <summary>
		/// Current in flight messages across all connections.
		/// </summary>
		public int TotalInFlightMessages { get; set; }

		/// <summary>
		/// Parked message stream URI.
		/// </summary>
		public string ParkedMessageUri { get; set; }

		/// <summary>
		/// Messages URI.
		/// </summary>
		public string GetMessagesUri { get; set; }
	}

	/// <summary>
	/// Configuration details of a persistent subscription.
	/// </summary>
	public sealed class PersistentSubscriptionConfigDetails {
		/// <summary>
		/// Whether to resolve LinkTos.
		/// </summary>
		public bool ResolveLinktos { get; set; }

		/// <summary>
		/// Which event to start from.
		/// </summary>
		public long StartFrom { get; set; }

		/// <summary>
		/// Message timeout in ms.
		/// </summary>
		public int MessageTimeoutMilliseconds { get; set; }

		/// <summary>
		/// Extra statistics.
		/// </summary>
		public bool ExtraStatistics { get; set; }

		/// <summary>
		/// Max retry count.
		/// </summary>
		public int MaxRetryCount { get; set; }

		/// <summary>
		/// Live buffer size.
		/// </summary>
		public int LiveBufferSize { get; set; }

		/// <summary>
		/// Buffer size.
		/// </summary>
		public int BufferSize { get; set; }

		/// <summary>
		/// Read buffer size.
		/// </summary>
		public int ReadBatchSize { get; set; }

		/// <summary>
		/// Checkpoint interval in ms.
		/// </summary>
		public int CheckPointAfterMilliseconds { get; set; }

		/// <summary>
		/// Min number of events between checkpoints.
		/// </summary>
		public int MinCheckPointCount { get; set; }

		/// <summary>
		/// Max number of events between checkpoints.
		/// </summary>
		public int MaxCheckPointCount { get; set; }

		/// <summary>
		/// Max subscribers permitted.
		/// </summary>
		public int MaxSubscriberCount { get; set; }

		/// <summary>
		/// Consumer strategy.
		/// </summary>
		public string NamedConsumerStrategy { get; set; }

		/// <summary>
		/// Whether to prefer round robin.
		/// </summary>
		public bool PreferRoundRobin { get; set; }
	}

	/// <summary>
	/// Details of a connection for a persistent subscription.
	/// </summary>
	public sealed class PersistentSubscriptionConnectionDetails {
		/// <summary>
		/// Origin of this connection.
		/// </summary>
		public string From { get; set; }

		/// <summary>
		/// Connection username.
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		/// Average events per second on this connection.
		/// </summary>
		public decimal AverageItemsPerSecond { get; set; }

		/// <summary>
		/// Total items on this connection.
		/// </summary>
		public long TotalItems { get; set; }

		/// <summary>
		/// Number of items seen since last measurement on this connection (used as the basis for <see cref="AverageItemsPerSecond"/>).
		/// </summary>
		public long CountSinceLastMeasurement { get; set; }

		/// <summary>
		/// Number of available slots.
		/// </summary>
		public int AvailableSlots { get; set; }

		/// <summary>
		/// Number of in flight messages on this connection.
		/// </summary>
		public int InFlightMessages { get; set; }
	}
}
