using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class MultiStreamMessageWriter : IMultiStreamMessageWriter
    {
        private readonly ILogger Log = LogManager.GetLoggerFor<MultiStreamMessageWriter>();
        private readonly IODispatcher _ioDispatcher;

        private readonly Dictionary<Guid, Queue> _queues = new Dictionary<Guid, Queue>();
        private IODispatcherAsync.CancellationScope _cancellationScope;

        public MultiStreamMessageWriter(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
            _cancellationScope = new IODispatcherAsync.CancellationScope();
        }

        public void Reset()
        {
            Log.Debug("PROJECTIONS: Resetting Worker Writer");
            _cancellationScope.Cancel();
            _cancellationScope = new IODispatcherAsync.CancellationScope();
            _queues.Clear();
        }

        public void PublishResponse(string command, Guid workerId, object body)
        {
            Queue queue;
            if (!_queues.TryGetValue(workerId, out queue))
            {
                queue = new Queue();
                _queues.Add(workerId, queue);
            }

            Log.Debug("PROJECTIONS: Scheduling the writing of {0} to {1}. Current status of Writer: Busy: {2}", command, "$projections-$" + workerId, queue.Busy);
            queue.Items.Add(new Queue.Item { Command = command, Body = body });
            if (!queue.Busy)
            {
                EmitEvents(queue, workerId);
            }
        }

        private void EmitEvents(Queue queue, Guid workerId)
        {
            queue.Busy = true;
            var events = queue.Items.Select(CreateEvent).ToArray();
            queue.Items.Clear();
            var streamId = "$projections-$" + workerId.ToString("N");
            _ioDispatcher.BeginWriteEvents(
                _cancellationScope,
                streamId,
                ExpectedVersion.Any,
                SystemAccount.Principal,
                events,
                completed =>
                {
                    queue.Busy = false;
                    if (completed.Result == OperationResult.Success)
                    {
                        Log.Debug("PROJECTIONS: Finished writing events to {0}: {1}", streamId, String.Join(",", events.Select(x => String.Format("{0}", x.EventType))));
                    }
                    else
                    {
                        var message = String.Format("PROJECTIONS: Failed writing events to {0} because of {1}: {2}", 
                            streamId, 
                            completed.Result, String.Join(",", events.Select(x => String.Format("{0}", x.EventType))));
                        Log.Debug(message); //Can't do anything about it, log and move on
                        //throw new Exception(message);
                    }

                    if (queue.Items.Count > 0)
                        EmitEvents(queue, workerId);
                    else
                        _queues.Remove(workerId);
                }).Run();
        }

        private Event CreateEvent(Queue.Item item)
        {
            return new Event(Guid.NewGuid(), item.Command, true, item.Body.ToJsonBytes(), null);
        }

        private class Queue
        {
            public bool Busy;
            public readonly List<Item> Items = new List<Item>();

            internal class Item
            {
                public string Command;
                public object Body;
            }
        }
    }
}
