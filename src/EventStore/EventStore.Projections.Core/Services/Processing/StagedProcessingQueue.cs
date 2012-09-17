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
    /// Stage dprocessing queue allows queued processing of multi-step tasks.  The 
    /// processing order allows multiple tasks to be processed at the same time with a constraint
    /// that all preceding tasks in the queue has already started processing at the given stage. 
    /// 
    /// For instance:  multiple foreach sub-projections can request state to be loaded, then they can process
    /// it and store.  But no subprojection can process events prior to preceding projections has completed processing. 
    /// </summary>
    public class StagedProcessingQueue
    {
        private class TaskEntry
        {
            public readonly StagedTask Task;
            public bool Busy;
            public bool Completed;
            public int ReadForStage;

            public TaskEntry(StagedTask task)
            {
                Task = task;
            }

            public override string ToString()
            {
                return string.Format("ReadForStage: {3},  Busy: {1}, Completed: {2} => {0}", Task, Busy, Completed, ReadForStage);
            }
        }

        private readonly bool[] _orderedStage;
        private readonly Queue<TaskEntry> _tasks = new Queue<TaskEntry>();
        private readonly HashSet<object> _precedingCorrelations = new HashSet<object>();

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
            _tasks.Enqueue(new TaskEntry(stagedTask));
        }

        public int Process()
        {
            int processed = 0;
            StagedTask taskProcessed = null;
            while (_tasks.Count > 0)
            {
                taskProcessed = ProcessOneTask(taskProcessed);
                if (taskProcessed == null)
                    break;
                processed++;
            }
            return processed;
        }

        private StagedTask ProcessOneTask(StagedTask runThisOnly)
        {
            while (_tasks.Count > 0 && _tasks.Peek().Completed)
                _tasks.Dequeue();

            _precedingCorrelations.Clear();
            var previousTaskMinimumProcessingStage  = int.MaxValue;
            var taskStage = int.MaxValue;
            foreach (var entry in _tasks)
            {
                previousTaskMinimumProcessingStage = Math.Min(taskStage, previousTaskMinimumProcessingStage);
                taskStage = entry.ReadForStage;

                if (_precedingCorrelations.Contains(entry.Task.CorrelationId))
                    break;
                _precedingCorrelations.Add(entry.Task.CorrelationId);

                if (entry.Busy)
                    continue;

                if (entry.Completed)
                    continue;

                if (_orderedStage[taskStage] && taskStage > previousTaskMinimumProcessingStage)
                    continue;

                if (runThisOnly != null && entry.Task != runThisOnly) // skip other tasks (this is to allow current entry continue to the next step - required by TickHandling)
                    break; // do not skip, we can process only in this order

                // here we should be at the first StagedTask of current processing level which is not busy
                entry.Busy = true;
                entry.Task.Process(taskStage, readyForStage => CompleteTaskProcessing(entry, readyForStage));
                return entry.Task;
            }
            return null;
        }

        private void CompleteTaskProcessing(TaskEntry entry, int readyForStage)
        {
            if (!entry.Busy)
                throw new InvalidOperationException("Task was not in progress");
            entry.Busy = false;
            if (readyForStage < 0)
                RemoveCompletedTask(entry);
            else
                entry.ReadForStage = readyForStage;
        }

        private void RemoveCompletedTask(TaskEntry entry)
        {
            entry.Completed = true;
        }
    }

    public abstract class StagedTask
    {
        public readonly object CorrelationId;

        protected StagedTask(object correlationId)
        {
            CorrelationId = correlationId;
        }

        public abstract void Process(int onStage, Action<int> readyForStage);
    }
}
