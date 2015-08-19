using System;
using System.Collections.Generic;
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
                if(CurrentClient.Push(ev))
                {
                    return ConsumerPushResult.Sent;
                }
                else
                {
                    MoveToNextClient();
                }
            }

            return ConsumerPushResult.NoMoreCapacity;
        }
    }

    class RoundRobinPersistentSubscriptionConsumerStrategy : IPersistentSubscriptionConsumerStrategy
    {
        protected readonly IList<PersistentSubscriptionClient> Clients = new List<PersistentSubscriptionClient>();
        private int _currentClientIndex;

        public virtual string Name
        {
            get { return SystemConsumerStrategies.RoundRobin; }
        }

        public PersistentSubscriptionClient CurrentClient
        {
            get { return Clients[_currentClientIndex]; }
        }

        public void ClientAdded(PersistentSubscriptionClient client)
        {
            Clients.Add(client);
        }

        public void ClientRemoved(PersistentSubscriptionClient client)
        {
            int indexOf = Clients.IndexOf(client);
            if (indexOf == -1)
            {
                throw new InvalidOperationException("Only added clients can be removed.");
            }

            Clients.RemoveAt(indexOf);
            if (_currentClientIndex > indexOf)
            {
                _currentClientIndex--;
            }

            if (_currentClientIndex >= Clients.Count)
            {
                _currentClientIndex = 0;
            }
        }

        public virtual ConsumerPushResult PushMessageToClient(ResolvedEvent ev)
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                bool pushed = Clients[_currentClientIndex].Push(ev);

                MoveToNextClient();

                if (pushed)
                {
                    return ConsumerPushResult.Sent;
                }
            }

            return ConsumerPushResult.NoMoreCapacity;
        }

        protected void MoveToNextClient()
        {
            _currentClientIndex = (_currentClientIndex + 1) % Clients.Count;
        }
    }

}