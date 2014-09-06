using System;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionEventLoader : IPersistentSubscriptionEventLoader
    {
        public const int PullBatchSize = 100;

        private readonly IODispatcher _ioDispatcher;
        private readonly int _maxPullBatchSize;

        public PersistentSubscriptionEventLoader(IODispatcher ioDispatcher, int maxPullBatchSize)
        {
            _ioDispatcher = ioDispatcher;
            _maxPullBatchSize = maxPullBatchSize;
        }

        public void BeginLoadState(PersistentSubscription_old subscription, int startEventNumber, int countToLoad, Action<ResolvedEvent[], int> onFetchCompleted)
        {
            _ioDispatcher.ReadForward(
                subscription.EventStreamId, startEventNumber, Math.Min(countToLoad, PullBatchSize),
                subscription.ResolveLinkTos, SystemAccount.Principal, new ResponseHandler(onFetchCompleted).FetchCompleted);
        }

        private class ResponseHandler
        {            
            private readonly Action<ResolvedEvent[], int> _onFetchCompleted;

            public ResponseHandler(Action<ResolvedEvent[], int> onFetchCompleted)
            {
                _onFetchCompleted = onFetchCompleted;
            }

            public void FetchCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg)
            {
                _onFetchCompleted(msg.Events, msg.NextEventNumber);
            }
        }
    }
}