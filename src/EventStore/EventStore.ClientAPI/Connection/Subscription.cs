using System;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Connection
{
    internal class Subscription
    {
        public readonly TaskCompletionSource<object> Source;

        public readonly Guid Id;
        public readonly string Stream;

        public readonly Action<RecordedEvent, Position> EventAppeared;
        public readonly Action SubscriptionDropped;

        public Subscription(TaskCompletionSource<object> source,
                            Guid id,
                            string stream,
                            Action<RecordedEvent, Position> eventAppeared,
                            Action subscriptionDropped)
        {
            Source = source;

            Id = id;
            Stream = stream;

            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
        }

        public Subscription(TaskCompletionSource<object> source,
                            Guid id,
                            Action<RecordedEvent, Position> eventAppeared,
                            Action subscriptionDropped)
        {
            Source = source;

            Id = id;
            Stream = null;

            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
        }
    }
}