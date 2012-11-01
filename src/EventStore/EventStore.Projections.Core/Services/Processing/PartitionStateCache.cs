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
        //NOTE: _partitionStates is locked only to allow statistics retrieval from another thread 
        private readonly int _maxCachedPartitions;

        private readonly Queue<Tuple<CheckpointTag, string>> _cacheOrder = new Queue<Tuple<CheckpointTag, string>>();

        private readonly Dictionary<string, Tuple<string, CheckpointTag, object>> _partitionStates =
            new Dictionary<string, Tuple<string, CheckpointTag, object>>();

        private CheckpointTag _unlockedBefore;

        public PartitionStateCache(int maxCachedPartitions = 1000)
        {
            _maxCachedPartitions = maxCachedPartitions;
        }

        public int CachedItemCount
        {
            get
            {
                lock (_partitionStates)
                    return _partitionStates.Count;
            }
        }

        public void Initialize()
        {
            lock(_partitionStates)
                _partitionStates.Clear();
            _cacheOrder.Clear();
            _unlockedBefore = null;
        }

        public void CacheAndLockPartitionState(string partition, string data, CheckpointTag at, object by = null)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            if (data == null) throw new ArgumentNullException("data");
            EnsureCanLockPartitionAt(partition, at);
            lock (_partitionStates)
                _partitionStates[partition] = Tuple.Create(data, at, by);
            if (!string.IsNullOrEmpty(partition)) // cached forever - for root state
                _cacheOrder.Enqueue(Tuple.Create(at, partition));
            CleanUp();
        }

        public string TryGetAndLockPartitionState(string partition, CheckpointTag at, object by = null)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            Tuple<string, CheckpointTag, object> stateData;
            lock (_partitionStates)
            {
                if (!_partitionStates.TryGetValue(partition, out stateData))
                    return null;
                EnsureCanLockPartitionAt(partition, at);
                if (at != null && at <= stateData.Item2)
                    throw new InvalidOperationException(
                        string.Format(
                            "Attempt to relock the '{0}' partition state locked at the '{1}' position at the earlier position '{2}'",
                            partition, stateData.Item2, at));
                _partitionStates[partition] = Tuple.Create(stateData.Item1, at, by);
            }
            if (!string.IsNullOrEmpty(partition)) // cached forever - for root state
                _cacheOrder.Enqueue(Tuple.Create(at, partition));
            CleanUp();
            return stateData.Item1;
        }

        public string GetLockedPartitionState(string partition)
        {
            Tuple<string, CheckpointTag, object> stateData;
            lock (_partitionStates)
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
                _cacheOrder.Dequeue();
                Tuple<string, CheckpointTag, object> entry;
                lock (_partitionStates)
                    if (!_partitionStates.TryGetValue(top.Item2, out entry))
                        continue; // already removed
                if (entry.Item2 >= _unlockedBefore)
                    continue; // was relocked
                lock (_partitionStates)
                    _partitionStates.Remove(top.Item2);
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
