// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class PartitionStateCache
    {
        private readonly int _maxCachedPartitions;

        private readonly List<Tuple<CheckpointTag, string>> _cacheOrder = new List<Tuple<CheckpointTag, string>>();

        private readonly Dictionary<string, Tuple<PartitionState, CheckpointTag>> _partitionStates =
            new Dictionary<string, Tuple<PartitionState, CheckpointTag>>();
        private int _cachedItemCount;

        private CheckpointTag _unlockedBefore;
        private readonly CheckpointTag _zeroPosition;

        public PartitionStateCache(CheckpointTag zeroPosition, int maxCachedPartitions = 1000)
        {
            _zeroPosition = zeroPosition;
            _unlockedBefore = zeroPosition;
            _maxCachedPartitions = maxCachedPartitions;
        }

        public int CachedItemCount
        {
            get { return _cachedItemCount; }
        }

        public void Initialize()
        {
            _partitionStates.Clear();
            _cachedItemCount = 0;

            _cacheOrder.Clear();
            _unlockedBefore = _zeroPosition;
        }

        public void CacheAndLockPartitionState(string partition, PartitionState data, CheckpointTag at)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            if (data == null) throw new ArgumentNullException("data");
            EnsureCanLockPartitionAt(partition, at);

            _partitionStates[partition] = Tuple.Create(data, at);
            _cachedItemCount = _partitionStates.Count;

            if (!string.IsNullOrEmpty(partition)) // cached forever - for root state
                _cacheOrder.Add(Tuple.Create(at, partition));
            CleanUp();
        }

        public void CachePartitionState(string partition, PartitionState data)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            if (data == null) throw new ArgumentNullException("data");
            if (partition == null) throw new ArgumentException("Root partition must be locked", "partition");

            _partitionStates[partition] = Tuple.Create(data, _zeroPosition);
            _cachedItemCount = _partitionStates.Count;

            _cacheOrder.Insert(0, Tuple.Create(_zeroPosition, partition));
            CleanUp();
        }

        public PartitionState TryGetAndLockPartitionState(string partition, CheckpointTag lockAt)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            Tuple<PartitionState, CheckpointTag> stateData;
            if (!_partitionStates.TryGetValue(partition, out stateData))
                return null;
            EnsureCanLockPartitionAt(partition, lockAt);
            if (lockAt != null && lockAt <= stateData.Item2)
                throw new InvalidOperationException(
                    string.Format(
                        "Attempt to relock the '{0}' partition state locked at the '{1}' position at the earlier position '{2}'",
                        partition, stateData.Item2, lockAt));

            _partitionStates[partition] = Tuple.Create(stateData.Item1, lockAt);
            _cachedItemCount = _partitionStates.Count;

            if (!string.IsNullOrEmpty(partition)) // cached forever - for root state
            _cacheOrder.Add(Tuple.Create(lockAt, partition));
            CleanUp();
            return stateData.Item1;
        }

        public PartitionState TryGetPartitionState(string partition)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            Tuple<PartitionState, CheckpointTag> stateData;
            if (!_partitionStates.TryGetValue(partition, out stateData))
                return null;
            return stateData.Item1;
        }

        public PartitionState GetLockedPartitionState(string partition)
        {
            Tuple<PartitionState, CheckpointTag> stateData;
            if (!_partitionStates.TryGetValue(partition, out stateData))
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Partition '{0}' state was requested as locked but it is missing in the cache.", partition));
            }
            if (stateData.Item2 != null && stateData.Item2 <= _unlockedBefore)
                throw new InvalidOperationException(
                    string.Format(
                        "Partition '{0}' state was requested as locked but it is cached as unlocked", partition));
            return stateData.Item1;
        }

        public void Unlock(CheckpointTag beforeCheckpoint)
        {
            _unlockedBefore = beforeCheckpoint;
            CleanUp();
        }

        private void CleanUp()
        {
            while (_cacheOrder.Count > _maxCachedPartitions*5 || CachedItemCount > _maxCachedPartitions)
            {
                if (_cacheOrder.Count == 0)
                    break;
                Tuple<CheckpointTag, string> top = _cacheOrder.FirstOrDefault();
                if (top.Item1 >= _unlockedBefore)
                    break; // other entries were locked after the checkpoint (or almost .. order is not very strong)
                _cacheOrder.RemoveAt(0);
                Tuple<PartitionState, CheckpointTag> entry;
                if (!_partitionStates.TryGetValue(top.Item2, out entry))
                    continue; // already removed
                if (entry.Item2 >= _unlockedBefore)
                    continue; // was relocked

                _partitionStates.Remove(top.Item2);
                _cachedItemCount = _partitionStates.Count;
            }
        }

        private void EnsureCanLockPartitionAt(string partition, CheckpointTag at)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            if (at == null && partition != "")
                throw new InvalidOperationException("Only the root partition can be locked forever");
            if (partition == "" && at != null)
                throw new InvalidOperationException("Root partition must be locked forever");
            if (at != null && at <= _unlockedBefore)
                throw new InvalidOperationException(
                    string.Format(
                        "Attempt to lock the '{0}' partition state at the position '{1}' before the unlocked position '{2}'",
                        partition, at, _unlockedBefore));
        }
    }
}
