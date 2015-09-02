using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription
{
    public enum ConsumerPushResult
    {
        Sent,
        NoMoreCapacity
    }

    public interface IPersistentSubscriptionConsumerStrategy
    {
        string Name { get; }

        void ClientAdded(PersistentSubscriptionClient client);

        void ClientRemoved(PersistentSubscriptionClient client);

        ConsumerPushResult PushMessageToClient(ResolvedEvent ev);
    }

    class DispatchToSinglePersistentSubscriptionConsumerStrategy : RoundRobinPersistentSubscriptionConsumerStrategy
    {
        public override string Name
        {
            get { return SystemConsumerStrategies.DispatchToSingle; }
        }

        public override ConsumerPushResult PushMessageToClient(ResolvedEvent ev)
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                if (Clients.Peek().Push(ev))
                {
                    return ConsumerPushResult.Sent;
                }
                var c = Clients.Dequeue();
                Clients.Enqueue(c);
            }

            return ConsumerPushResult.NoMoreCapacity;
        }
    }

    class RoundRobinPersistentSubscriptionConsumerStrategy : IPersistentSubscriptionConsumerStrategy
    {
        protected readonly Queue<PersistentSubscriptionClient> Clients = new Queue<PersistentSubscriptionClient>();

        public virtual string Name
        {
            get { return SystemConsumerStrategies.RoundRobin; }
        }

        public void ClientAdded(PersistentSubscriptionClient client)
        {
            Clients.Enqueue(client);
        }

        public void ClientRemoved(PersistentSubscriptionClient client)
        {
            if (!Clients.Contains(client))
            {
                throw new InvalidOperationException("Only added clients can be removed.");
            }
            var temp = Clients.ToList();
            var indexOf = temp.IndexOf(client);
            temp.RemoveAt(indexOf);
            Clients.Clear();
            foreach (var persistentSubscriptionClient in temp)
            {
                Clients.Enqueue(persistentSubscriptionClient);
            }
        }

        public virtual ConsumerPushResult PushMessageToClient(ResolvedEvent ev)
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                var c = Clients.Dequeue();
                var pushed = c.Push(ev);
                Clients.Enqueue(c);
                if (pushed)
                {
                    return ConsumerPushResult.Sent;
                }
            }
            return ConsumerPushResult.NoMoreCapacity;
        }
    }

}