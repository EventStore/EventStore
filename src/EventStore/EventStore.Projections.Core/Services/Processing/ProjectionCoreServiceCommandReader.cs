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
using EventStore.Common.Utils;
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

        public ProjectionCoreServiceCommandReader(IPublisher publisher, IODispatcher ioDispatcher)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");

            _coreServiceId = Guid.NewGuid().ToString("N");
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
                case "$start":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<StartCommand>();
                    _publisher.Publish(new CoreProjectionManagementMessage.Start(Guid.ParseExact(commandBody.Id, "N")));
                    break;
                }
                case "$stop":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<StopCommand>();
                    _publisher.Publish(new CoreProjectionManagementMessage.Stop(Guid.ParseExact(commandBody.Id, "N")));
                    break;
                }
                case "$kill":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<KillCommand>();
                    _publisher.Publish(new CoreProjectionManagementMessage.Kill(Guid.ParseExact(commandBody.Id, "N")));
                    break;
                }
                case "$dispose":
                {
                    var commandBody = resolvedEvent.Event.Data.ParseJson<DisposeCommand>();
                    _publisher.Publish(new CoreProjectionManagementMessage.Dispose(Guid.ParseExact(commandBody.Id, "N")));
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

        private class StartCommand
        {
            public string Id { get; set; }
        }

        private class StopCommand
        {
            public string Id { get; set; }
        }

        private class KillCommand
        {
            public string Id { get; set; }
        }

        private class DisposeCommand
        {
            public string Id { get; set; }
        }
    }
}
