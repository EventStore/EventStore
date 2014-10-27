using System;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionStreamReader : IPersistentSubscriptionStreamReader
    {
        public const int MaxPullBatchSize = 500;

        private readonly IODispatcher _ioDispatcher;
        private readonly int _maxPullBatchSize;

        public PersistentSubscriptionStreamReader(IODispatcher ioDispatcher, int maxPullBatchSize)
        {
            _ioDispatcher = ioDispatcher;
            _maxPullBatchSize = maxPullBatchSize;
        }

        public void BeginReadEvents(string stream, int startEventNumber, int countToLoad, int batchSize, bool resolveLinkTos,
            Action<ResolvedEvent[], int, bool> onEventsFound)
        {
            var actualBatchSize = GetBatchSize(batchSize);
            _ioDispatcher.ReadForward(
                stream, startEventNumber, Math.Min(countToLoad, actualBatchSize),
                resolveLinkTos, SystemAccount.Principal, new ResponseHandler(onEventsFound).FetchCompleted);
        }

        private int GetBatchSize(int batchSize)
        {
            return Math.Min(Math.Min(batchSize, MaxPullBatchSize), _maxPullBatchSize);
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
                //TODO check error codes?
                Console.WriteLine(msg.Result);
                _onFetchCompleted(msg.Events, msg.NextEventNumber, msg.IsEndOfStream);
            }
        }
    }
}