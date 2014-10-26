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
        private readonly Queue<OutstandingMessage> _regular = new Queue<OutstandingMessage>();

        private readonly BoundedQueue<OutstandingMessage> _liveBuffer;
        private bool _live;

        public int LiveBufferCount { get { return _liveBuffer.Count; } }
        public int BufferCount { get { return _retry.Count + _regular.Count; } }
        public bool Live { get { return _live; } }

        public bool CanAccept(int count)
        {
            return _maxBufferSize - BufferCount > count;
        }

        public StreamBuffer(int maxBufferSize, int maxLiveBufferSize, int initialSequence, bool startInHistory)
        {
            _live = !startInHistory;
            _initialSequence = initialSequence;
            _maxBufferSize = maxBufferSize;
            _liveBuffer = new BoundedQueue<OutstandingMessage>(maxLiveBufferSize);
        }

        private void SwitchToLive()
        {
            while (_liveBuffer.Count > 0)
            {
                _regular.Enqueue(_liveBuffer.Dequeue());
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
            _retry.Enqueue(ev);    
        }

        public void AddLiveMessage(OutstandingMessage ev)
        {
            if(_live) 
                _regular.Enqueue(ev);
            else
                _liveBuffer.Enqueue(ev);
        }

        public void AddReadMessage(OutstandingMessage ev)
        {
            if (_live) return;
            if (ev.ResolvedEvent.OriginalEventNumber <= _initialSequence)
                return;
            if (ev.ResolvedEvent.OriginalEventNumber < TryPeekLive())
            {
                _regular.Enqueue(ev);
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
            if (_regular.Count <= 0) return false;
            ev = _regular.Dequeue();
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
            if (_regular.Count <= 0) return false;
            ev = _regular.Peek();
            return true;
        }

        public void MoveToLive()
        {
            if (_liveBuffer.Count == 0) _live = true;
        }

        public int GetLowestRetry()
        {
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