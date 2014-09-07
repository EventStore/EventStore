using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class StreamBuffer
    {
        private readonly int _maxBufferSize;
        private int _lastKnownSequence;
        private readonly PreemptableQueue<OutstandingMessage> _buffer;
        private readonly BoundedQueue<OutstandingMessage> _liveBuffer;
        private bool _live;

        public int LiveBufferCount { get { return _liveBuffer.Count; } }
        public int BufferCount { get { return _buffer.Count; } }
        public bool Live { get { return _live; } }

        public bool CanAccept(int count)
        {
            return _maxBufferSize - _buffer.Count > count;
        }

        public StreamBuffer(int maxBufferSize, int maxLiveBufferSize, int lastKnownSequence, bool startFromBeginning)
        {
            _live = !startFromBeginning;
            _lastKnownSequence = lastKnownSequence;
            _maxBufferSize = maxBufferSize;
            _buffer = new PreemptableQueue<OutstandingMessage>();
            _liveBuffer = new BoundedQueue<OutstandingMessage>(maxLiveBufferSize);
        }

        private void SwitchToLive()
        {
            while (_liveBuffer.Count > 0)
            {
                _buffer.Enqueue(_liveBuffer.Dequeue());
            }
            _live = true;
        }
        
        private void DrainLiveTo(int eventNumber)
        {
            while (_liveBuffer.Count > 0 && _liveBuffer.Peek().ResolvedEvent.OriginalEventNumber < eventNumber)
            {
                _liveBuffer.Dequeue();
            }
        }

        public void AddRetry(OutstandingMessage ev)
        {
            _buffer.Preempt(ev);    
        }

        public void AddLiveMessage(OutstandingMessage ev)
        {
            if(_live) 
                _buffer.Enqueue(ev);
            else
                _liveBuffer.Enqueue(ev);
        }

        public void AddReadMessage(OutstandingMessage ev)
        {
            if (_live) return;
            if (ev.ResolvedEvent.OriginalEventNumber <= _lastKnownSequence)
                return;
            if (ev.ResolvedEvent.OriginalEventNumber < TryPeekLive())
            {
                _buffer.Enqueue(ev);
            }
            else if (ev.ResolvedEvent.OriginalEventNumber > TryPeekLive())
            {
                DrainLiveTo(ev.ResolvedEvent.OriginalEventNumber);
                SwitchToLive();
            }
            else 
            {
                SwitchToLive();
            }
        }

        private int TryPeekLive()
        {
            if(_liveBuffer.Count == 0) return int.MaxValue;
            return _liveBuffer.Peek().ResolvedEvent.OriginalEventNumber;
        }

        public bool TryDequeue(out OutstandingMessage ev)
        {
            if (_buffer.Count == 0)
            {
                ev = new OutstandingMessage();
                return false;
            }
            ev = _buffer.Dequeue();
            return true;
        }

        public bool TryPeek(out OutstandingMessage ev)
        {
            if (_buffer.Count == 0)
            {
                ev = new OutstandingMessage();
                return false;
            }
            ev = _buffer.Peek();
            return true;
        }
    }

    public enum BufferedStreamReaderState
    {
        Unknown,
        CatchingUp,
        Live
    }
}