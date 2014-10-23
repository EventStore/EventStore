using System;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionStreamReader : IPersistentSubscriptionStreamReader
    {
        public const int PullBatchSize = 100;

        private readonly IODispatcher _ioDispatcher;
        private readonly int _maxPullBatchSize;

        public PersistentSubscriptionStreamReader(IODispatcher ioDispatcher, int maxPullBatchSize)
        {
            _ioDispatcher = ioDispatcher;
            _maxPullBatchSize = maxPullBatchSize;
        }

        public void BeginReadEvents(string stream, int startEventNumber, int countToLoad, int batchSize, bool resolveLinkTos,
            Action<ResolvedEvent[], int, bool> onFetchCompleted)
        {
            //TODO implement reading loop here.
            _ioDispatcher.ReadForward(
                stream, startEventNumber, Math.Min(countToLoad, PullBatchSize),
                resolveLinkTos, SystemAccount.Principal, new ResponseHandler(onFetchCompleted).FetchCompleted);
        }

        private class ResponseHandler
        {            
            private readonly Action<ResolvedEvent[], int, bool> _onFetchCompleted;

            public ResponseHandler(Action<ResolvedEvent[], int, bool> onFetchCompleted)
            {
                _onFetchCompleted = onFetchCompleted;
            }

            public void FetchCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg)
            {
                _onFetchCompleted(msg.Events, msg.NextEventNumber, msg.IsEndOfStream);
            }
        }
    }
}