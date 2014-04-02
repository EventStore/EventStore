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
    public sealed class CommandWriter : ICommandWriter
    {
        private readonly IODispatcher _ioDispatcher;
        private readonly ILogger _logger = LogManager.GetLoggerFor<CommandWriter>();

        private readonly Dictionary<Guid, Queue> _queues = new Dictionary<Guid, Queue>();

        public CommandWriter(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
        }

        public void PublishCommand(string command, Guid workerId, object body)
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
            var streamId = "$projections-$" + workerId.ToString("N");
            _ioDispatcher.Perform(
                _ioDispatcher.BeginWriteEvents(
                    streamId,
                    ExpectedVersion.Any,
                    SystemAccount.Principal,
                    events,
                    completed =>
                    {
                        if (completed.Result != OperationResult.Success)
                        {
                            var message = string.Format(
                                "Cannot write commands to the stream {0}. status: {1}",
                                streamId,
                                completed.Result);
                            _logger.Fatal(message);
                            throw new Exception(message);
                        }

                        queue.Busy = false;
                        if (queue.Items.Count > 0)
                            EmitEvents(queue, workerId);
                        else
                            _queues.Remove(workerId);
                    }));
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