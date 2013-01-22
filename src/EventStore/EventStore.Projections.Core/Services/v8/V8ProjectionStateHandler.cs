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
using System.Globalization;
using System.Runtime.Serialization;
using EventStore.Core.Util;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;

namespace EventStore.Projections.Core.Services.v8
{
    public class V8ProjectionStateHandler : IProjectionStateHandler
    {
        private readonly PreludeScript _prelude;
        private readonly QueryScript _query;
        private List<EmittedEvent> _emittedEvents;
        private CheckpointTag _eventPosition;
        private bool _disposed;

        public V8ProjectionStateHandler(
            string preludeName, string querySource, Func<string, Tuple<string, string>> getModuleSource,
            Action<string> logger)
        {
            var preludeSource = getModuleSource(preludeName);
            var prelude = new PreludeScript(preludeSource.Item1, preludeSource.Item2, getModuleSource, logger);
            QueryScript query;
            try
            {
                query = new QueryScript(prelude, querySource, "POST-BODY");
                query.Emit += QueryOnEmit;
            }
            catch
            {
                prelude.Dispose(); // clean up unmanaged resources if failed to create
                throw;
            }
            _prelude = prelude;
            _query = query;

        }

        [DataContract]
        public class EmittedEventJsonContract
        {

            [DataMember] public string streamId;

            [DataMember] public string eventName;

            [DataMember] public string body;

        }


        private void QueryOnEmit(string json)
        {
            EmittedEventJsonContract emittedEvent;
            try
            {
                emittedEvent = json.ParseJson<EmittedEventJsonContract>();
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Failed to deserialize emitted event JSON", ex);
            }
            if (_emittedEvents == null)
                _emittedEvents = new List<EmittedEvent>();
            _emittedEvents.Add(new EmittedEvent(emittedEvent.streamId, Guid.NewGuid(), emittedEvent.eventName, emittedEvent.body, _eventPosition, expectedTag: null));
        }

        public void ConfigureSourceProcessingStrategy(QuerySourceProcessingStrategyBuilder builder)
        {
            CheckDisposed();
            var sourcesDefintion = _query.GetSourcesDefintion();
            if (sourcesDefintion == null)
                throw new InvalidOperationException("Invalid query.  No source definition.");
            if (sourcesDefintion.AllStreams)
                builder.FromAll();
            else
            {
                if (sourcesDefintion.Streams != null)
                    foreach (var stream in sourcesDefintion.Streams)
                        builder.FromStream(stream);
                if (sourcesDefintion.Categories != null)
                    foreach (var category in sourcesDefintion.Categories)
                        builder.FromCategory(category);
            }
            if (sourcesDefintion.AllEvents)
                builder.AllEvents();
            else
                if (sourcesDefintion.Events != null)
                    foreach (var @event in sourcesDefintion.Events)
                        builder.IncludeEvent(@event);
            if (sourcesDefintion.ByStreams)
                builder.SetByStream();
            if (sourcesDefintion.ByCustomPartitions)
                builder.SetByCustomPartitions();
            if (!string.IsNullOrWhiteSpace(sourcesDefintion.Options.StateStreamName))
                builder.SetStateStreamNameOption(sourcesDefintion.Options.StateStreamName);
            if (!string.IsNullOrWhiteSpace(sourcesDefintion.Options.ForceProjectionName))
                builder.SetForceProjectionName(sourcesDefintion.Options.ForceProjectionName);
            if (sourcesDefintion.Options.UseEventIndexes)
                builder.SetUseEventIndexes(true);
            if (sourcesDefintion.Options.ReorderEvents)
                builder.SetReorderEvents(true);
            if (sourcesDefintion.Options.ProcessingLag != null)
                builder.SetProcessingLag(sourcesDefintion.Options.ProcessingLag.GetValueOrDefault());
            if (sourcesDefintion.Options.EmitStateUpdated)
                builder.SetEmitStateUpdated(true);
        }

        public void Load(string state)
        {
            CheckDisposed();
            _query.SetState(state);
        }

        public void Initialize()
        {
            CheckDisposed();
            _query.Initialize();
        }

        public string GetStatePartition(
            CheckpointTag eventPosition, string streamId, string eventType, string category, Guid eventid,
            int sequenceNumber, string metadata, string data)
        {
            CheckDisposed();
            if (eventType == null)
                throw new ArgumentNullException("eventType");
            if (streamId == null)
                throw new ArgumentNullException("streamId");
            var partition = _query.GetPartition(
                data.Trim(), // trimming data passed to a JS 
                new string[] { streamId, eventType, category ?? "", sequenceNumber.ToString(CultureInfo.InvariantCulture), metadata ?? "", eventPosition.ToJson() });
            if (partition == "")
                return null;
            else 
                return partition;
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string streamId, string eventType, string category, Guid eventid,
            int sequenceNumber, string metadata, string data, out string newState, out EmittedEvent[] emittedEvents)
        {
            CheckDisposed();
            if (eventType == null)
                throw new ArgumentNullException("eventType");
            if (streamId == null)
                throw new ArgumentNullException("streamId");
            _eventPosition = eventPosition;
            _emittedEvents = null;
            _query.Push(
                data.Trim(), // trimming data passed to a JS 
                new[]
                    {
                        streamId, eventType, category ?? "", sequenceNumber.ToString(CultureInfo.InvariantCulture),
                        metadata ?? "", partition, eventPosition.ToJson()
                    });
            newState = _query.GetState();
            emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
            return true;
        }

        private void CheckDisposed()
        {
            if (_disposed)
                throw new InvalidOperationException("Disposed");
        }

        public void Dispose()
        {
            _disposed = true;
            if (_query != null)
                _query.Dispose();
            if (_prelude != null)
                _prelude.Dispose();
        }
    }
}
