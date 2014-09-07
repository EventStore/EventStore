using EventStore.Core.Data;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class SubscriptionBuffer
    {
        private readonly PersistentSubscriptionStats _statistics;
        private PersistentSubscriptionState _state;
        private readonly BoundedQueue<ResolvedEvent> _liveSubscriptionMessages;

        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        private readonly PersistentSubscription _parentSubscription;
        private bool _startFromBeginning = false;
        public PersistentSubscriptionState State { get { return _state; } }


        public SubscriptionBuffer(PersistentSubscription parentSubscription, int liveSubscriptionBufferSize, PersistentSubscriptionStats statistics, IPersistentSubscriptionCheckpointReader checkpointReader)
        {
            _parentSubscription = parentSubscription;
            _statistics = statistics;
            _checkpointReader = checkpointReader;
            _liveSubscriptionMessages = new BoundedQueue<ResolvedEvent>(liveSubscriptionBufferSize);
        }

        public void AddLiveMessage(ResolvedEvent ev)
        {
            _liveSubscriptionMessages.Enqueue(ev);
        }

        public void ReadNextNOrLessMessages(int count)
        {
            
        }

        public void Start()
        {
            _checkpointReader.BeginLoadState(_parentSubscription.SubscriptionId, OnStateLoaded);
        }

        private void OnStateLoaded(int? lastProcessedEvent)
        {
            if (lastProcessedEvent.HasValue)
            {
                _statistics.SetLastKnownEventNumber(lastProcessedEvent.Value);
                _state = PersistentSubscriptionState.Pull;
                FetchNewEventsBatch();
            }
            else
            {
                if (_startFromBeginning)
                {
                    _state = PersistentSubscriptionState.Pull;
                    _statistics.SetLastKnownEventNumber(-1);
                    FetchNewEventsBatch();
                }
                else
                {
                    _state = PersistentSubscriptionState.Push;
                }

            }
        }


        private void FetchNewEventsBatch()
        {
            //_outstandingFetchRequest = true;
            //_eventLoader.BeginLoadState(this, _nextEventNumber, inFlight, HandleReadEvents);
        }

        public void HandleReadEvents(ResolvedEvent[] events)
        {
            //callback from read
        }


        public void Shutdown()
        {
            _state = PersistentSubscriptionState.ShuttingDown;
        }

        public int GetLowestPosition()
        {
            return 0;
        }
    }
}