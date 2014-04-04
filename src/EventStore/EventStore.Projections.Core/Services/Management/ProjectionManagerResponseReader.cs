using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management
{
    public class ProjectionManagerResponseReader : IHandle<ProjectionManagementMessage.Starting>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private bool _stopped;

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
            ClientMessage.ReadStreamEventsBackwardCompleted response = null;
            yield return
                _ioDispatcher.BeginReadBackward(
                    "$projections-$master",
                    -1,
                    1,
                    false,
                    SystemAccount.Principal,
                    completed => response = completed);

            var from = response.LastEventNumber;

            while (!_stopped)
            {
                var eof = false;
                var subscribeFrom = default(TFPos);
                do
                {
                    yield return
                        _ioDispatcher.BeginReadForward(
                            "$projections-$master",
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
                yield return _ioDispatcher.BeginSubscribeAwake("$projections-$master", subscribeFrom, message => { });
            }
        }

        private void PublishCommand(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            var command = resolvedEvent.Event.EventType;
            switch (command)
            {
                case "$prepared":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ProjectionCoreResponseWriter.Prepared>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Prepared(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.SourceDefinition));
                    break;
                }
                case "$faulted":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ProjectionCoreResponseWriter.Faulted>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Faulted(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.FaultedReason));
                    break;
                }
                case "$started":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ProjectionCoreResponseWriter.Started>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Started(Guid.ParseExact(commandBody.Id, "N")));
                    break;
                }
                case "$statistics-report":
                {
                    var commandBody =
                        resolvedEvent.Event.Data.ParseJson<ProjectionCoreResponseWriter.StatisticsReport>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.StatisticsReport(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Statistcs));
                    break;
                }
                case "$stopped":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<ProjectionCoreResponseWriter.Stopped>();
                    _publisher.Publish(
                        new CoreProjectionManagementMessage.Stopped(
                            Guid.ParseExact(commandBody.Id, "N"),
                            commandBody.Completed));
                    break;
                }
                default:
                    throw new Exception("Unknown response: " + command);
            }
        }

    }
}
