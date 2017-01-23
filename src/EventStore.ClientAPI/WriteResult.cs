using EventStore.ClientAPI.Common.Utils;
namespace EventStore.ClientAPI
{
    /// <summary>
    /// Result type returned after writing to a stream.
    /// </summary>
    public struct WriteResult
    {
        /// <summary>
        /// The stream to which this <see cref="WriteResult"/> pertains.
        /// </summary>
        public readonly string StreamName;

        /// <summary>
        /// The next expected version for the stream.
        /// </summary>
        public readonly int NextExpectedVersion;

        /// <summary>
        /// The <see cref="LogPosition"/> of the write.
        /// </summary>
        public readonly Position LogPosition;
    
        /// <summary>
        /// Constructs a new <see cref="WriteResult"/>.
        /// </summary>
        /// <param name="nextExpectedVersion">The next expected version for the stream.</param>
        /// <param name="logPosition">The position of the write in the log</param>
        /// <param name="streamName">The name of the stream to which this <see cref="WriteResult"/> pertains.</param>
        public WriteResult(int nextExpectedVersion, Position logPosition, string streamName =null)
        {
            StreamName = streamName;
            NextExpectedVersion = nextExpectedVersion;
            LogPosition = logPosition;
        }
    }
}