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
    /// <summary>
    /// Staged processing queue allows queued processing of multi-step tasks.  The 
    /// processing order allows multiple tasks to be processed at the same time with a constraint
    /// a) ordered stage: all preceding tasks in the queue has already started processing at the given stage. 
    /// b) unordered stage: no items with the same correlation_id are in the queue before current item
    /// 
    /// For instance:  multiple foreach sub-projections can request state to be loaded, then they can process
    /// it and store.  But no subprojection can process events prior to preceding projections has completed processing. 
    /// </summary>
    public class StagedProcessingQueue
    {
        private class TaskEntry
        {
            public readonly StagedTask Task;
            public readonly long Sequence;
            public bool Busy;
            public object BusyCorrelationId;
            public TaskEntry PreviousByCorrelation;
            public TaskEntry NextByCorrelation;
            public bool Completed;
            public int ReadForStage;

            public TaskEntry(StagedTask task, long sequence)
            {
                Task = task;
                ReadForStage = -1;
                Sequence = sequence;
            }

            public override string ToString()
            {
                return string.Format("ReadForStage: {3},  Busy: {1}, Completed: {2} => {0}", Task, Busy, Completed, ReadForStage);
            }
        }

        private readonly bool[] _orderedStage;
        private readonly Queue<TaskEntry> _tasks = new Queue<TaskEntry>();
        private readonly Dictionary<object, TaskEntry> _correlationLastEntries = new Dictionary<object, TaskEntry>();
        private long _sequence = 0;

        public StagedProcessingQueue(bool[] orderedStage)
        {
            _orderedStage = orderedStage.ToArray();
        }

        public bool IsEmpty
        {
            get { return _tasks.Count == 0; }
        }

        public int Count
        {
            get { return _tasks.Count; }
        }

        public void Enqueue(StagedTask stagedTask)
        {
            var entry = new TaskEntry(stagedTask, ++_sequence);
            SetEntryCorrelation(entry, stagedTask.InitialCorrelationId);
            entry.ReadForStage = 0;
            _tasks.Enqueue(entry);
        }

        public int Process()
        {
            int processed = 0;
            TaskEntry taskProcessed = null;
            while (_tasks.Count > 0)
            {
                RemoveCompleted();
                var entry = GetEntryToProcess(taskProcessed);
                if (entry == null)
                    break;
                ProcessEntry(entry);
                taskProcessed = entry;
                processed++;
            }
            return processed;
        }

        private void ProcessEntry(TaskEntry entry)
        {
            entry.Task.Process(
                entry.ReadForStage,
                (readyForStage, newCorrelationId) => CompleteTaskProcessing(entry, readyForStage, newCorrelationId));
        }

        private TaskEntry GetEntryToProcess(TaskEntry runThisOnly)
        {

            var previousTaskMinimumProcessingStage  = int.MaxValue;
            var taskStage = int.MaxValue;
            foreach (var entry in _tasks)
            {
                previousTaskMinimumProcessingStage = Math.Min(taskStage, previousTaskMinimumProcessingStage);
                taskStage = entry.ReadForStage;

                if (entry.PreviousByCorrelation != null)
                    break;

                if (entry.Busy)
                    continue;

                if (entry.Completed)
                    continue;

                if (_orderedStage[taskStage] && taskStage > previousTaskMinimumProcessingStage)
                    continue;

                if (runThisOnly != null && entry != runThisOnly) // skip other tasks (this is to allow current entry continue to the next step - required by TickHandling)
                    break; // do not skip, we can process only in this order

                // here we should be at the first StagedTask of current processing level which is not busy
                entry.Busy = true;
                return entry;
            }
            return null;
        }

        private void RemoveCompleted()
        {
            while (_tasks.Count > 0 && _tasks.Peek().Completed)
            {
                var task = _tasks.Dequeue();
                if (task.BusyCorrelationId != null)
                {
                    var nextByCorrelation = task.NextByCorrelation;
                    if (nextByCorrelation != null)
                    {
                        if (nextByCorrelation.PreviousByCorrelation != task)
                            throw new Exception("Invalid linked list by correlation");
                        task.NextByCorrelation = null;
                        nextByCorrelation.PreviousByCorrelation = null;
                    }
                    else
                    {
                        // remove the last one
                        _correlationLastEntries.Remove(task.BusyCorrelationId); 
                    }
                }
            }
        }

        private void CompleteTaskProcessing(TaskEntry entry, int readyForStage, object newCorrelationId)
        {
            if (!entry.Busy)
                throw new InvalidOperationException("Task was not in progress");
            entry.Busy = false;
            SetEntryCorrelation(entry, newCorrelationId);
            if (readyForStage < 0)
                RemoveCompletedTask(entry);
            else
                entry.ReadForStage = readyForStage;
        }

        private void SetEntryCorrelation(TaskEntry entry, object newCorrelationId)
        {
            if (!Equals(entry.BusyCorrelationId, newCorrelationId))
            {
                if (entry.ReadForStage != -1 && !_orderedStage[entry.ReadForStage])
                    throw new InvalidOperationException("Cannot set busy correlation id at non-ordered stage");
                if (entry.BusyCorrelationId != null)
                    throw new InvalidOperationException("Busy correlation id has been already set");

                entry.BusyCorrelationId = newCorrelationId;
                if (newCorrelationId != null)
                {
                    TaskEntry lastEntry;
                    if (_correlationLastEntries.TryGetValue(newCorrelationId, out lastEntry))
                    {
                        if (entry.Sequence < lastEntry.Sequence)
                            //NOTE: should never happen as we require ordered stage or initialization
                            throw new InvalidOperationException("Cannot inject task correlation id before another task with the same correlation id");
                        lastEntry.NextByCorrelation = entry;
                        entry.PreviousByCorrelation = lastEntry;
                        _correlationLastEntries[newCorrelationId] = entry;
                    }
                    else
                        _correlationLastEntries.Add(newCorrelationId, entry);
                }
            }
        }

        private void RemoveCompletedTask(TaskEntry entry)
        {
            entry.Completed = true;
        }

        public void Initialize()
        {
            _tasks.Clear();
        }
    }

    public abstract class StagedTask
    {
        public readonly object InitialCorrelationId;

        protected StagedTask(object initialCorrelationId)
        {
            InitialCorrelationId = initialCorrelationId;
        }

        public abstract void Process(int onStage, Action<int, object> readyForStage);

    }
}
