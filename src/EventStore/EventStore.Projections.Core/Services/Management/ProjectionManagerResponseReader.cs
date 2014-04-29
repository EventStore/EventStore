using System;
using System.Collections.Generic;
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services.Management
{
    //TODO: response reader must start before Manager (otherwise misses first responses at least in case with pre-registered workers)
    public class ProjectionManagerResponseReader : IHandle<ProjectionManagementMessage.Starting>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;

        public ProjectionManagerResponseReader(IPublisher publisher, IODispatcher ioDispatcher)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");

            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
        }

        public void Handle(ProjectionManagementMessage.Starting message)
        {
            _ioDispatcher.Perform(PerformStartReader());
        }

        private IEnumerable<IODispatcher.Step> PerformStartReader()
        {
            ClientMessage.WriteEventsCompleted writeResult = null;
//            Trace.WriteLine("Writing $response-reader-starting");
            yield return
                _ioDispatcher.BeginWriteEvents(
                    ProjectionNamesBuilder._projectionsMasterStream,
                    ExpectedVersion.Any,
                    SystemAccount.Principal,
                    new[] { new Event(Guid.NewGuid(), "$response-reader-starting", true, "{}", null) },
                    completed => writeResult = completed);

            if (writeResult.Result != OperationResult.Success)
                throw new Exception("Cannot start response reader. Write result: " + writeResult.Result);

            var from = writeResult.LastEventNumber;
//            Trace.WriteLine("$response-reader-starting has been written. Starting event number is: " + from);


//            Trace.WriteLine("Writing $response-reader-started");
            yield return
                _ioDispatcher.BeginWriteEvents(
                    ProjectionNamesBuilder._projectionsControlStream,
                    ExpectedVersion.Any,
                    SystemAccount.Principal,
                    new[] { new Event(Guid.NewGuid(), "$response-reader-started", true, "{}", null) },
                    completed => writeResult = completed);

            if (writeResult.Result != OperationResult.Success)
                throw new Exception("Cannot start response reader. Write result: " + writeResult.Result);

//            Trace.WriteLine("$response-reader-started has been written");

            while (true)
            {
                var eof = false;
                var subscribeFrom = default(TFPos);
                do
                {
                    //Trace.WriteLine("Reading " + ProjectionNamesBuilder._projectionsMasterStream);
                    yield return
                        _ioDispatcher.BeginReadForward(
                            ProjectionNamesBuilder._projectionsMasterStream,
                            from,
                            10,
                            false,
                            SystemAccount.Principal,
                            completed =>
                            {
                                //Trace.WriteLine(ProjectionNamesBuilder._projectionsMasterStream + " read completed: " + completed.Result);
                                if (completed.Result == ReadStreamResult.Success
                                    || completed.Result == ReadStreamResult.NoStream)
                                {
                                    from = completed.NextEventNumber == -1 ? 0 : completed.NextEventNumber;
                                    eof = completed.IsEndOfStream;
                                    // subscribeFrom is only used if eof
                                    subscribeFrom = new TFPos(
                                        completed.TfLastCommitPosition,
                                        completed.TfLastCommitPosition);
                                    if (completed.Result == ReadStreamResult.Success)
                                    {
                                        foreach (var e in completed.Events)
                                            PublishCommand(e);
                                    }
                                }
                                else
                                    Trace.WriteLine(ProjectionNamesBuilder._projectionsMasterStream + " read completed: " + completed.Result);
                            });
                     

                } while (!eof);
                //Trace.WriteLine("Awaiting " + ProjectionNamesBuilder._projectionsMasterStream);
                yield return _ioDispatcher.BeginSubscribeAwake(ProjectionNamesBuilder._projectionsMasterStream, subscribeFrom, message => { });
                //Trace.WriteLine(ProjectionNamesBuilder._projectionsMasterStream + " await completed");
            }
        }

        private void PublishCommand(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            var command = resolvedEvent.Event.EventType;
            //Trace.WriteLine("Response received: " + command);
            switch (command)
            {
                case "$response-reader-starting":
                    break;
                case "$projection-worker-started":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ProjectionWorkerStarted>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.ProjectionWorkerStarted(
                            Guid.ParseExact(commandBody.Id, "N")));
                    break;
                }
                case "$prepared":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<Prepared>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.Prepared(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.SourceDefinition));
                    break;
                }
                case "$faulted":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<Faulted>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.Faulted(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.FaultedReason));
                    break;
                }
                case "$started":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<Started>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.Started(Guid.ParseExact(commandBody.Id, "N")));
                    break;
                }
                case "$statistics-report":
                {
                    var commandBody =
                        resolvedEvent.Event.Data.ParseJson<StatisticsReport>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.StatisticsReport(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Statistics,
                            -1));
                    break;
                }
                case "$stopped":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<Stopped>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.Stopped(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Completed));
                    break;
                }
                case "$state":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<StateReport>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.StateReport(
                            Guid.ParseExact(commandBody.CorrelationId, "N"),
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Partition,
                            commandBody.State,
                            commandBody.Position));
                    break;
                }
                case "$result":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ResultReport>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.ResultReport(
                            Guid.ParseExact(commandBody.CorrelationId, "N"),
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Partition,
                            commandBody.Result,
                            commandBody.Position));
                    break;
                }
                case "$slave-projection-reader-assigned":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<SlaveProjectionReaderAssigned>();
                    _publisher.Publish(
                        new CoreProjectionStatusMessage.SlaveProjectionReaderAssigned(
                            Guid.ParseExact(commandBody.Id, "N"),
                            Guid.ParseExact(commandBody.SubscriptionId, "N")));
                    break;
                }
                case "$abort":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<AbortCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.Abort(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs));
                    break;
                }
                case "$disable":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<DisableCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.Disable(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs));
                    break;
                }
                case "$enable":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<EnableCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.Enable(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs));
                    break;
                }
                case "$get-query":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<GetQueryCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.GetQuery(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs));
                    break;
                }
                case "$get-result":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<GetResultCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.GetResult(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.Partition));
                    break;
                }
                case "$get-state":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<GetStateCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.GetState(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.Partition));
                    break;
                }
                case "$get-statistics":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<GetStatisticsCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.GetStatistics(
                            new NoopEnvelope(),
                            commandBody.Mode,
                            commandBody.Name,
                            commandBody.IncludeDeleted));
                    break;
                }
                case "$post":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<PostCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.Post(
                            new NoopEnvelope(),
                            commandBody.Mode,
                            commandBody.Name,
                            commandBody.RunAs,
                            commandBody.HandlerType,
                            commandBody.Query,
                            commandBody.Enabled,
                            commandBody.CheckpointsEnabled,
                            commandBody.EmitEnabled,
                            commandBody.EnableRunAs));
                    break;
                }
                case "$reset":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ResetCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.Reset(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs));
                    break;
                }
                case "$set-runas":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<SetRunAsCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.SetRunAs(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs,
                            commandBody.SetRemove));
                    break;
                }
                case "$start-slave-projections":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<StartSlaveProjectionsCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.StartSlaveProjections(
                            new PublishEnvelope(_publisher),
                            commandBody.RunAs,
                            commandBody.Name,
                            commandBody.SlaveProjections,
                            Guid.ParseExact(commandBody.MasterWorkerId, "N"),
                            Guid.ParseExact(commandBody.MasterCorrelationId, "N")));
                    break;
                }
                case "$delete":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<DeleteCommand>();
                    _publisher.Publish(
                        new ProjectionManagementMessage.Command.Delete(
                            new NoopEnvelope(),
                            commandBody.Name,
                            commandBody.RunAs,
                            commandBody.DeleteCheckpointStream,
                            commandBody.DeleteStateStream));
                    break;
                }
                default:
                    throw new Exception("Unknown response: " + command);
            }
        }

    }
}
