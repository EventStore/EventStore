using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.PersistentSubscription
{
    public interface IPersistentSubscriptionMessageParker
    {
        void BeginParkMessage(ResolvedEvent @event, string reason, Action<ResolvedEvent, OperationResult> completed);
        void BeginReadEndSequence(Action<int?> completed);
        void BeginMarkParkedMessagesReprocessed(int sequence);

    }
}