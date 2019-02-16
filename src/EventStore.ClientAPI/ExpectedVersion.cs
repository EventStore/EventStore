namespace EventStore.ClientAPI {
	/// <summary>
	/// Constants used for expected version control
	/// </summary>
	/// <remarks>
	/// The use of expected version can be a bit tricky especially when discussing idempotency assurances given by Event Store.
	///
	/// There are four possible values you can use for passing an expected version.
	/// Any other value states that the last event written to the stream should have a sequence number matching your
	/// expected value.
	///
	/// Event Store assures idempotency for all operations using any value in ExpectedVersion except for
	/// ExpectedVersion.Any and ExpectedVersion.StreamExists. When using ExpectedVersion.Any or ExpectedVersion.StreamExists
	/// Event Store does its best to assure idempotency but does not guarantee idempotency.
	/// </remarks>
	public static class ExpectedVersion {
		/// <summary>
		/// The write should not conflict with anything and should always succeed.
		/// </summary>
		public const int Any = -2;

		/// <summary>
		/// The stream should not yet exist. If it does exist treat that as a concurrency problem.
		/// </summary>
		public const int NoStream = -1;

		/// <summary>
		/// The stream should exist but be empty when writing. If it does not exist or is not empty treat that as a concurrency problem.
		/// </summary>
		public const int EmptyStream = -1;

		/// <summary>
		/// The stream should exist. If it or a metadata stream does not exist treat that as a concurrency problem.
		/// </summary>
		public const int StreamExists = -4;
	}
}
