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
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCoreServiceCommandReader
        : IHandle<ProjectionCoreServiceMessage.StartCore>, IHandle<ProjectionCoreServiceMessage.StopCore>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private readonly string _coreServiceId;
        private bool _stopped;

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
        }

        private IEnumerable<IODispatcher.Step> PerformStartCore()
        {

            var events = new[]
            {new Event(Guid.NewGuid(), "$projection-worker-started", true, "{\"id\":\"" + _coreServiceId + "\"}", null)};
            ClientMessage.WriteEventsCompleted response = null;
            yield return
                _ioDispatcher.BeginWriteEvents(
                    "$projections-$master",
                    ExpectedVersion.Any,
                    SystemAccount.Principal,
                    events,
                    r => response = r);

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
                    var commandBody = resolvedEvent.Event.Data.ParseJson <LoadStoppedCommand>();
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

        public class LoadStoppedCommand
        {
            public string Id { get; set; }
        }

        public class StartCommand
        {
            public string Id { get; set; }
        }

        public class StopCommand
        {
            public string Id { get; set; }
        }

        public class KillCommand
        {
            public string Id { get; set; }
        }

        public class DisposeCommand
        {
            public string Id { get; set; }
        }

        public struct PersistedProjectionVersion
        {
            public int Id;
            public int Epoch;
            public int Version;

            public static implicit operator ProjectionVersion(PersistedProjectionVersion source)
            {
                return new ProjectionVersion(source.Id, source.Epoch, source.Version);
            }

            public static implicit operator PersistedProjectionVersion(ProjectionVersion source)
            {
                return new PersistedProjectionVersion
                {
                    Epoch = source.Epoch,
                    Id = source.ProjectionId,
                    Version = source.Version
                };
            }
        }

        public sealed class PersistedProjectionConfig
        {
            public string RunAs;
            public string[] RunAsRoles;
            public int CheckpointHandledThreshold;
            public int CheckpointUnhandledBytesThreshold;
            public int PendingEventsThreshold;
            public int MaxWriteBatchLength;
            public bool EmitEventEnabled;
            public bool CheckpointsEnabled;
            public bool CreateTempStreams;
            public bool StopOnEof;
            public bool IsSlaveProjection;

            public PersistedProjectionConfig()
            {
            }

            public PersistedProjectionConfig(ProjectionConfig config)
            {
                RunAs = config.RunAs.Identity.Name;
                RunAsRoles = config.RunAs == SystemAccount.Principal
                    ? new string[0]
                    : ((OpenGenericPrincipal) config.RunAs).Roles;

                CheckpointHandledThreshold = config.CheckpointHandledThreshold;
                CheckpointUnhandledBytesThreshold = config.CheckpointUnhandledBytesThreshold;
                PendingEventsThreshold = config.PendingEventsThreshold;
                MaxWriteBatchLength = config.MaxWriteBatchLength;
                EmitEventEnabled = config.EmitEventEnabled;
                CheckpointsEnabled = config.CheckpointsEnabled;
                CreateTempStreams = config.CreateTempStreams;
                StopOnEof = config.StopOnEof;
                IsSlaveProjection = config.IsSlaveProjection;
            }

            public ProjectionConfig ToConfig()
            {
                return
                    new ProjectionConfig(
                        (RunAs == SystemAccount.Principal.Identity.Name)
                            ? (IPrincipal) SystemAccount.Principal
                            : new OpenGenericPrincipal(RunAs, RunAsRoles),
                        CheckpointHandledThreshold,
                        CheckpointUnhandledBytesThreshold,
                        PendingEventsThreshold,
                        MaxWriteBatchLength,
                        EmitEventEnabled,
                        CheckpointsEnabled,
                        CreateTempStreams,
                        StopOnEof,
                        IsSlaveProjection);
            }
        }

        public class CreatePreparedCommand
        {
            public string Id { get; set; }
            public PersistedProjectionConfig Config { get; set; }
            public QuerySourcesDefinition SourceDefinition { get; set; }
            public PersistedProjectionVersion Version { get; set; }
            public string HandlerType { get; set; }
            public string Query { get; set; }
            public string Name { get; set; }
        }

        public class CreateAndPrepareCommand
        {
            public string Id { get; set; }
            public PersistedProjectionConfig Config { get; set; }
            public PersistedProjectionVersion Version { get; set; }
            public string HandlerType { get; set; }
            public string Query { get; set; }
            public string Name { get; set; }
        }

        public class CreateAndPrepareSlaveCommand
        {
            public string Id { get; set; }
            public string MasterCoreProjectionId { get; set; }
            public string MasterWorkerId { get; set; }
            public PersistedProjectionConfig Config { get; set; }
            public PersistedProjectionVersion Version { get; set; }
            public string HandlerType { get; set; }
            public string Query { get; set; }
            public string Name { get; set; }
        }

        public class SpoolStreamReadingCommand
        {
            public string SubscriptionId { get; set; }
            public string StreamId { get; set; }
            public int CatalogSequenceNumber { get; set; }
            public long LimitingCommitPosition { get; set; }
        }

        public sealed class GetStateCommand
        {
            public string Id { get; set; }
            public string CorrelationId { get; set; }
            public string Partition { get; set; }
        }

        public sealed class GetResultCommand
        {
            public string Id { get; set; }
            public string CorrelationId { get; set; }
            public string Partition { get; set; }
        }
    }
}
