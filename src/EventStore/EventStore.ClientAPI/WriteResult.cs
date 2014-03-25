namespace EventStore.ClientAPI
{
    /// <summary>
    /// Result type returned after writing to a stream.
    /// </summary>
    public struct WriteResult
    {
        /// <summary>
        /// The next expected version for the stream.
        /// </summary>
        public readonly int NextExpectedVersion;

        /// <summary>
        /// Constructs a new <see cref="WriteResult"/>.
        /// </summary>
        /// <param name="nextExpectedVersion">The next expected version for the stream.</param>
        public WriteResult(int nextExpectedVersion)
        {
            NextExpectedVersion = nextExpectedVersion;
        }
    }
}