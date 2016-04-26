namespace EventStore.ClientAPI
{
    /// <summary>
    /// Constants used for expected version control
    /// </summary>
    /// <remarks>
    /// The use of expected version can be a bit tricky especially when discussing idempotency assurances given by the event store.
    /// 
    /// There are five possible values that can be used for the passing of an expected version.
    /// 
    /// ExpectedVersion.Any (-2) says that you should not conflict with anything.
    /// ExpectedVersion.NoStream (-1) says that the stream should not exist when doing your write.
    /// ExpectedVersion.EmptyStream (0) says the stream should exist but be empty when doing the write.
    /// ExpectedVersion.StreamExists(-4) says the stream or a metadata stream should exist when doing your write.
    /// 
    /// Any other value states that the last event written to the stream should have a sequence number matching your 
    /// expected value.
    /// 
    /// The Event Store will assure idempotency for all operations using any value in ExpectedVersion except for
    /// ExpectedVersion.Any and ExpectedVersion.StreamExists. When using ExpectedVersion.Any or ExpectedVersion.StreamExists
    /// the Event Store will do its best to assure idempotency but will not guarantee idempotency.
    /// </remarks>
    public static class ExpectedVersion
    {
        /// <summary>
        /// This write should not conflict with anything and should always succeed.
        /// </summary>
        public const int Any = -2;
        /// <summary>
        /// The stream being written to should not yet exist. If it does exist treat that as a concurrency problem.
        /// </summary>
        public const int NoStream = -1;
        /// <summary>
        /// The stream should exist and should be empty. If it does not exist or is not empty treat that as a concurrency problem.
        /// </summary>
        public const int EmptyStream = -1;

        /// <summary>
        /// The stream should exist. If it or a metadata stream does not exist treat that as a concurrency problem.
        /// </summary>
        public const int StreamExists = -4;
    }
}