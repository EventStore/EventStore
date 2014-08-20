using System;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a subscription to a single stream or to the stream
    /// of all events in the Event Store.
    /// </summary>
    public class EventStoreSubscription : IDisposable
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
        private readonly Action _unsubscribe;

        internal EventStoreSubscription(Action unsubscribe, string streamId, long lastCommitPosition, int? lastEventNumber)
        {
            Ensure.NotNull(unsubscribe, "unsubscribe");

            _unsubscribe = unsubscribe;
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

        /// <summary>
        /// Unsubscribes from the stream.
        /// </summary>
        public void Unsubscribe()
        {
            _unsubscribe();
        }
    }
}
