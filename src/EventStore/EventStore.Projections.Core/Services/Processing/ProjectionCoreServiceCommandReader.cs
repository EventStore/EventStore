using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCoreServiceCommandReader
        : IHandle<ProjectionCoreServiceMessage.StartCore>, IHandle<ProjectionCoreServiceMessage.StopCore>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private readonly string _coreServiceId;
        private bool _stopped;
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreServiceCommandReader>();

        public ProjectionCoreServiceCommandReader(IPublisher publisher, IODispatcher ioDispatcher, string workerId)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");

            _coreServiceId = workerId;
            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
        }

        public void Handle(ProjectionCoreServiceMessage.StartCore message)
        {
            _ioDispatcher.Perform(PerformStartCore());
            _ioDispatcher.Perform(PerformControl());
        }

        private IEnumerable<IODispatcher.Step> PerformControl()
        {
            ClientMessage.ReadStreamEventsBackwardCompleted readResult = null;
            yield return
                _ioDispatcher.BeginReadBackward(
                    ProjectionNamesBuilder._projectionsControlStream,
                    -1,
                    1,
                    false,
                    SystemAccount.Principal,
                    completed => readResult = completed);


            int fromEventNumber;

            if (readResult.Result == ReadStreamResult.NoStream)
            {
                fromEventNumber = 0;
            }
            else
            {
                if (readResult.Result != ReadStreamResult.Success)
                    throw new Exception("Cannot start control reader. Read result: " + readResult.Result);

                fromEventNumber = readResult.LastEventNumber + 1;
            }

            Trace.WriteLine("Starting read control from: " + fromEventNumber);

            //TODO: handle shutdown here and in other readers
            long subscribeFrom = 0;
            var doWriteRegistration = true;
            while (true)
            {
                if (doWriteRegistration)
                {
                    var events = new[]
                    {
                        new Event(
                            Guid.NewGuid(),
                            "$projection-worker-started",
                            true,
                            "{\"id\":\"" + _coreServiceId + "\"}",
                            null)
                    };
                    Trace.WriteLine("Registering worker " + _coreServiceId);
                    ClientMessage.WriteEventsCompleted response = null;
                    yield return
                        _ioDispatcher.BeginWriteEvents(
                            ProjectionNamesBuilder._projectionsMasterStream,
                            ExpectedVersion.Any,
                            SystemAccount.Principal,
                            events,
                            r => response = r);
                    Trace.WriteLine("Worker registered: " + response.Result);
                }
                do
                {
                    ClientMessage.ReadStreamEventsForwardCompleted readResultForward = null;
                    yield return
                        _ioDispatcher.BeginReadForward(
                            ProjectionNamesBuilder._projectionsControlStream,
                            fromEventNumber,
                            1,
                            false,
                            SystemAccount.Principal,
                            completed => readResultForward = completed);
                    Trace.WriteLine("Control stream read forward result: " + readResultForward.Result);

                    if (readResultForward.Result != ReadStreamResult.Success
                        && readResultForward.Result != ReadStreamResult.NoStream)
                        throw new Exception("Control reader failed. Read result: " + readResultForward.Result);
                    if (readResultForward.Events != null && readResultForward.Events.Length > 0)
                    {
                        doWriteRegistration =
                            readResultForward.Events.Any(v => v.Event.EventType == "$response-reader-started");
                        fromEventNumber = readResultForward.NextEventNumber;
                        subscribeFrom = readResultForward.TfLastCommitPosition;
                        break;
                    }
                    if (readResultForward.Result == ReadStreamResult.Success)
                        subscribeFrom = readResultForward.TfLastCommitPosition;
                    Trace.WriteLine("Awaiting control stream");

                    yield return
                        _ioDispatcher.BeginSubscribeAwake(
                            ProjectionNamesBuilder._projectionsControlStream,
                            new TFPos(subscribeFrom, subscribeFrom),
                            message => { });
                    Trace.WriteLine("Control stream await completed");

                } while (true);


            }
        }

        private IEnumerable<IODispatcher.Step> PerformStartCore()
        {

            var from = 0;
            while (!_stopped)
            {
                var eof = false;
                var subscribeFrom = default(TFPos);
                do
                {
                    yield return
                        _ioDispatcher.BeginReadForward(
                            "$projections-$" + _coreServiceId,
                            from,
                            10,
                            false,
                            SystemAccount.Principal,
                            completed =>
                            {
                                from = completed.NextEventNumber == -1 ? 0 : completed.NextEventNumber;
                                eof = completed.IsEndOfStream;
                                // subscribeFrom is only used if eof
                                subscribeFrom = new TFPos(
                                    completed.TfLastCommitPosition,
                                    completed.TfLastCommitPosition);
                                foreach (var e in completed.Events)
                                    PublishCommand(e);
                            });


                } while (!eof);
                yield return
                    _ioDispatcher.BeginSubscribeAwake("$projections-$" + _coreServiceId, subscribeFrom, message => { });
            }
        }

        private void PublishCommand(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            var command = resolvedEvent.Event.EventType;
            _logger.Trace("RCVD: " + command + resolvedEvent.Event.Data.FromUtf8());
            switch (command)
            {
                case "$create-prepared":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<CreatePreparedCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.CreatePrepared(
                            Guid.ParseExact(commandBody.Id, "N"),
                            Guid.Empty,
                            commandBody.Name,
                            commandBody.Version,
                            commandBody.Config.ToConfig(),
                            commandBody.SourceDefinition,
                            commandBody.HandlerType,
                            commandBody.Query));
                    break;
                }
                case "$create-and-prepare":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<CreateAndPrepareCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.CreateAndPrepare(
                            Guid.ParseExact(commandBody.Id, "N"),
                            Guid.Empty,
                            commandBody.Name,
                            commandBody.Version,
                            commandBody.Config.ToConfig(),
                            commandBody.HandlerType,
                            commandBody.Query));
                    break;
                }
                case "$create-and-prepare-slave":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<CreateAndPrepareSlaveCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.CreateAndPrepareSlave(
                            Guid.ParseExact(commandBody.Id, "N"),
                            Guid.Empty,
                            commandBody.Name,
                            commandBody.Version,
                            commandBody.Config.ToConfig(),
                            Guid.ParseExact(commandBody.MasterWorkerId, "N"),
                            Guid.ParseExact(commandBody.MasterCoreProjectionId, "N"),
                            commandBody.HandlerType,
                            commandBody.Query));
                    break;
                }
                case "$spool-stream-reading":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<SpoolStreamReadingCommand>();
                    _publisher.Publish(
                        new ReaderSubscriptionManagement.SpoolStreamReadingCore(
                            Guid.ParseExact(commandBody.SubscriptionId, "N"),
                            commandBody.StreamId,
                            commandBody.CatalogSequenceNumber,
                            commandBody.LimitingCommitPosition));
                    break;
                }
                case "$load-stopped":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<LoadStoppedCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.LoadStopped(
                            Guid.ParseExact(commandBody.Id, "N"),
                            Guid.Empty));
                    break;
                }
                case "$start":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<StartCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Start(Guid.ParseExact(commandBody.Id, "N"), Guid.Empty));
                    break;
                }
                case "$stop":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<StopCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Stop(Guid.ParseExact(commandBody.Id, "N"), Guid.Empty));
                    break;
                }
                case "$kill":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<KillCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Kill(Guid.ParseExact(commandBody.Id, "N"), Guid.Empty));
                    break;
                }
                case "$dispose":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<DisposeCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Dispose(Guid.ParseExact(commandBody.Id, "N"), Guid.Empty));
                    break;
                }
                case "$get-state":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<GetStateCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.GetState(
                            Guid.ParseExact(commandBody.CorrelationId, "N"),
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Partition,
                            Guid.Empty));
                    break;
                }
                case "$get-result":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<GetResultCommand>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.GetResult(
                            Guid.ParseExact(commandBody.CorrelationId, "N"),
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Partition,
                            Guid.Empty));
                    break;
                }
                default:
                    throw new Exception("Unknown command: " + command);
            }
        }

        public void Handle(ProjectionCoreServiceMessage.StopCore message)
        {
            _stopped = true;
        }
    }
}
