using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Checkpoint
{
    public class InMemMultiCheckpoint : IMultiCheckpoint
    {
        public string Name { get { return _name; } }

        public int CheckpointCount { get { return _curCount; } }

        public IEnumerable<Tuple<long, IPEndPoint>> CurrentCheckpoints
        {
            get
            {
                for (int i=0; i<_curCount; ++i)
                {
                    yield return Checkpoints[i];
                }
            }
        }

        internal readonly Tuple<long, IPEndPoint>[] Checkpoints;

        private readonly string _name;
        private int _curCount;

        public InMemMultiCheckpoint(int bestCount): this(Guid.NewGuid().ToString(), bestCount)
        {
        }

        public InMemMultiCheckpoint(string name, int bestCount)
        {
            Ensure.NotNull(name, "name");
            Ensure.Positive(bestCount, "bestCount");

            _name = name;
            Checkpoints = new Tuple<long, IPEndPoint>[bestCount];
            _curCount = 0;
        }

        public void Dispose()
        {
            // NOOP
        }

        public void Close()
        {
            // NOOP
        }

        public void Flush()
        {
            // NOOP
        }

        public void Update(IPEndPoint endPoint, long checkpoint)
        {
            int i;
            for (i = 0; i < _curCount; ++i)
            {
                if (Checkpoints[i].Item2.Equals(endPoint)) // if IPEndPoint is already there
                {
                    var check = Tuple.Create(checkpoint, endPoint);
                    while (i+1 < _curCount && check.Item1 < Checkpoints[i+1].Item1)
                    {
                        Checkpoints[i] = Checkpoints[i + 1];
                        i += 1;
                    }
                    while (i-1 >= 0 && check.Item1 > Checkpoints[i-1].Item1)
                    {
                        Checkpoints[i] = Checkpoints[i - 1];
                        i -= 1;
                    }
                    Checkpoints[i] = check;
                    return;
                }
            }

            for (i = 0; i < _curCount; ++i)
            {
                if (Checkpoints[i].Item1 <= checkpoint)
                    break;
            }

            if (i < _curCount || _curCount < Checkpoints.Length)
            {
                if (_curCount < Checkpoints.Length)
                    _curCount += 1;

                for (int j = _curCount - 1; j - 1 >= i; --j)
                {
                    Checkpoints[j] = Checkpoints[j - 1]; // shift right
                }
                Checkpoints[i] = Tuple.Create(checkpoint, endPoint);
            }

            // checkpoint is too small, we just ignore it
        }

        public void Clear()
        {
            _curCount = 0;
        }

        public bool TryGetMinMax(out long checkpoint)
        {
            if (_curCount == 0)
            {
                checkpoint = 0;
                return false;
            }
            checkpoint = Checkpoints[_curCount-1].Item1; // smallest checkpoint
            return true;
        }
    }
}