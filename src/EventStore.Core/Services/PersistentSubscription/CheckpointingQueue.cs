using System;
using System.Collections.Generic;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class CheckpointingQueue
    {
        private readonly SortedList<long, ResolvedEvent> _list = new SortedList<long, ResolvedEvent>();
        private readonly Action<int> _checkpointCallback;

        public CheckpointingQueue(Action<int> checkpointCallback)
        {
            _checkpointCallback = checkpointCallback;
        }

        public void Enqueue(SequencedEvent sequencedEvent)
        {
            _list.Add(sequencedEvent.Sequence, sequencedEvent.Event);
        }

        public void MarkCheckpoint()
        {
            var index = 1;
            var skipIndex = -1L;
            var toRemove = new List<long>();
            var keys = _list.Keys;
            while (index < keys.Count)
            {
                var current = keys[index];
                var prev = keys[index - 1];
                if (current != prev + 1)
                {
                    break;
                }
                skipIndex = prev;
                toRemove.Add(skipIndex);
                index++;
            }
            if (skipIndex != -1L)
            {
                var eventNumber = _list[skipIndex].Event.EventNumber;
                foreach (var key in toRemove)
                {
                    _list.Remove(key);
                }
                _checkpointCallback(eventNumber);
            }
        }
    }
}