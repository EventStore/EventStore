using System;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a subscription to a single stream or to the stream
    /// of all events in the Event Store.
    /// </summary>
    public abstract class EventStoreSubscription : IDisposable
    {
        /// <summary>
        /// True if this subscription is to all streams.
        /// </summary>
        public bool IsSubscribedToAll { get { return _streamId == string.Empty; } }
        /// <summary>
        /// The name of the stream to which the subscription is subscribed.
        /// </summary>
        public string StreamId { get { return _streamId; } }
        /// <summary>
        /// The last commit position seen on the subscription (if this is
        /// a subscription to all events).
        /// </summary>
        public readonly long LastCommitPosition;
        /// <summary>
        /// The last event number seen on the subscription (if this is a
        /// subscription to a single stream).
        /// </summary>
        public readonly int? LastEventNumber;

        private readonly string _streamId;

        internal EventStoreSubscription(string streamId, long lastCommitPosition, int? lastEventNumber)
        {
            _streamId = streamId;
            LastCommitPosition = lastCommitPosition;
            LastEventNumber = lastEventNumber;
        }

        /// <summary>
        /// Unsubscribes from the stream.
        /// </summary>
        public void Dispose()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Unsubscribes from the stream.
        /// </summary>
        public void Close()
        {
            Unsubscribe();
        }

        ///<summary>
        ///Unsubscribes from the stream
        ///</summary>
        public abstract void Unsubscribe();
    }
}