using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class MultiStreamMessageWriter : IMultiStreamMessageWriter
    {
        private readonly IODispatcher _ioDispatcher;
        private readonly ILogger _logger = LogManager.GetLoggerFor<MultiStreamMessageWriter>();

        private readonly Dictionary<Guid, Queue> _queues = new Dictionary<Guid, Queue>();
        private IODispatcherAsync.CancellationScope _cancellationScope;

        public MultiStreamMessageWriter(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
            _cancellationScope = new IODispatcherAsync.CancellationScope();
        }

        public void PublishResponse(string command, Guid workerId, object body)
        {
            Queue queue;
            if (!_queues.TryGetValue(workerId, out queue))
            {
                queue = new Queue();
                _queues.Add(workerId, queue);
            }

            queue.Items.Add(new Queue.Item {Command = command, Body = body});
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
            foreach (var e in events)
            {
              DebugLogger.Log("Writing a {0} command to {1}", e.EventType, streamId);
            }
            _ioDispatcher.BeginWriteEvents(
                _cancellationScope,
                streamId,
                ExpectedVersion.Any,
                SystemAccount.Principal,
                events,
                completed =>
                {
                    DebugLogger.Log("Writing to {0} completed", streamId);
                    queue.Busy = false;
                    if (completed.Result != OperationResult.Success)
                    {
                        var message = string.Format(
                            "Cannot write commands to the stream {0}. status: {1}",
                            streamId,
                            completed.Result);
                        _logger.Fatal(message);
                        throw new Exception(message);
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
