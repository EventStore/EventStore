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
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;

namespace EventStore.Projections.Core.Services.v8
{
    public class V8ProjectionStateHandler : IProjectionStateHandler
    {
        private readonly PreludeScript _prelude;
        private readonly QueryScript _query;
        private List<EmittedEvent> _emittedEvents;

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
            _emittedEvents.Add(new EmittedEvent(emittedEvent.streamId, Guid.NewGuid(), emittedEvent.eventName, emittedEvent.body));
        }

        public void ConfigureSourceProcessingStrategy(QuerySourceProcessingStrategyBuilder builder)
        {
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
            if (!string.IsNullOrWhiteSpace(sourcesDefintion.Options.StateStreamName))
                builder.SetStateStreamNameOption(sourcesDefintion.Options.StateStreamName);
            if (!string.IsNullOrWhiteSpace(sourcesDefintion.Options.ForceProjectionName))
                builder.SetForceProjectionName(sourcesDefintion.Options.ForceProjectionName);
        }

        public void Load(string state)
        {
            _query.SetState(state);
        }

        public void Initialize()
        {
            _query.Initialize();
        }

        public bool ProcessEvent(
            EventPosition position, string streamId, string eventType, string category, Guid eventid,
            int sequenceNumber, string metadata, string data, out string newState, out EmittedEvent[] emittedEvents)
        {
            if (eventType == null)
                throw new ArgumentNullException("eventType");
            if (streamId == null)
                throw new ArgumentNullException("streamId");
            _emittedEvents = null;
            _query.Push(
                data.Trim(), // trimming data passed to a JS 
                new string[] {streamId, eventType, category ?? "", sequenceNumber.ToString(CultureInfo.InvariantCulture), metadata ?? "", position.PreparePosition.ToString()});
            newState = _query.GetState();
            emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
            return true;
        }

        public void Dispose()
        {
            if (_query != null)
                _query.Dispose();
            if (_prelude != null)
                _prelude.Dispose();
        }
    }
}
