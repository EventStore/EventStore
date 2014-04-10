using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class ResponseWriter : IResponseWriter
    {
        private readonly IODispatcher _ioDispatcher;
        private readonly ILogger _logger = LogManager.GetLoggerFor<ResponseWriter>();

        private bool Busy;
        private readonly List<Item> Items = new List<Item>();

        private class Item
        {
            public string Command;
            public object Body;
        }

        public ResponseWriter(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
        }

        public void PublishCommand(string command, object body)
        {

            Items.Add(new Item {Command = command, Body = body});
            if (!Busy)
            {
                EmitEvents();
            }
        }

        private void EmitEvents()
        {
            Busy = true;
            var events = Items.Select(CreateEvent).ToArray();
            Items.Clear();
            var streamId = ProjectionNamesBuilder._projectionsMasterStream;
            _ioDispatcher.Perform(
                _ioDispatcher.BeginWriteEvents(
                    streamId,
                    ExpectedVersion.Any,
                    SystemAccount.Principal,
                    events,
                    completed =>
                    {
                        Busy = false;
                        if (completed.Result != OperationResult.Success)
                        {
                            var message = string.Format(
                                "Cannot write commands to the stream {0}. status: {1}",
                                streamId,
                                completed.Result);
                            _logger.Fatal(message);
                            throw new Exception(message);
                        }

                        if (Items.Count > 0)
                            EmitEvents();
                    }));
        }

        private Event CreateEvent(Item item)
        {
            return new Event(Guid.NewGuid(), item.Command, true, item.Body.ToJsonBytes(), null);
        }

    }
}