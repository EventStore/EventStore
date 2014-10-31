using System.Collections.Generic;
using System.Linq;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class StreamBuffer
    {
        private readonly int _maxBufferSize;
        private readonly int _initialSequence;
        private readonly Queue<OutstandingMessage> _retry = new Queue<OutstandingMessage>();
        private readonly Queue<OutstandingMessage> _buffer = new Queue<OutstandingMessage>();

        private readonly BoundedQueue<OutstandingMessage> _liveBuffer;

        public int LiveBufferCount { get { return _liveBuffer.Count; } }
        public int BufferCount { get { return _retry.Count + _buffer.Count; } }
        public int RetryBufferCount { get { return _retry.Count; } }
        public int ReadBufferCount { get { return _buffer.Count; } }
        public bool Live { get; private set; }

        public bool CanAccept(int count)
        {
            return _maxBufferSize - BufferCount > count;
        }

        public StreamBuffer(int maxBufferSize, int maxLiveBufferSize, int initialSequence, bool startInHistory)
        {
            Live = !startInHistory;
            _initialSequence = initialSequence;
            _maxBufferSize = maxBufferSize;
            _liveBuffer = new BoundedQueue<OutstandingMessage>(maxLiveBufferSize);
        }

        private void SwitchToLive()
        {
            while (_liveBuffer.Count > 0)
            {
                _buffer.Enqueue(_liveBuffer.Dequeue());
            }
            Live = true;
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
            _retry.Enqueue(ev);    
        }

        public void AddLiveMessage(OutstandingMessage ev)
        {
            if (Live)
            {
                if (_buffer.Count < _maxBufferSize)
                    _buffer.Enqueue(ev);
                else
                    Live = false;
            }
            _liveBuffer.Enqueue(ev);
        }

        public void AddReadMessage(OutstandingMessage ev)
        {
            if (Live) return;
            if (ev.ResolvedEvent.OriginalEventNumber <= _initialSequence)
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
            return _liveBuffer.Count == 0 ? int.MaxValue : _liveBuffer.Peek().ResolvedEvent.OriginalEventNumber;
        }

        public bool TryDequeue(out OutstandingMessage ev)
        {
            ev = new OutstandingMessage();
            if (_retry.Count > 0)
            {
                ev = _retry.Dequeue();
                return true;
            }
            if (_buffer.Count <= 0) return false;
            ev = _buffer.Dequeue();
            return true;
        }

        public bool TryPeek(out OutstandingMessage ev)
        {
            ev = new OutstandingMessage();
            if (_retry.Count > 0)
            {
                ev = _retry.Peek();
                return true;
            }
            if (_buffer.Count <= 0) return false;
            ev = _buffer.Peek();
            return true;
        }

        public void MoveToLive()
        {
            if (_liveBuffer.Count == 0) Live = true;
        }

        public int GetLowestRetry()
        {
            if (_retry.Count == 0) return int.MaxValue;
            return _retry.Min(x => x.ResolvedEvent.OriginalEventNumber);
        }
    }

    public enum BufferedStreamReaderState
    {
        Unknown,
        CatchingUp,
        Live
    }
}