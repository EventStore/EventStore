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
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace EventStore.Projections.Core.Services.v8
{
    public class V8ProjectionStateHandler : IProjectionStateHandler
    {
        private readonly PreludeScript _prelude;
        private readonly QueryScript _query;
        private List<EmittedEventEnvelope> _emittedEvents;
        private CheckpointTag _eventPosition;
        private bool _disposed;

        public V8ProjectionStateHandler(
            string preludeName, string querySource, Func<string, Tuple<string, string>> getModuleSource,
            Action<string> logger, Action<int, Action> cancelCallbackFactory)
        {
            var preludeSource = getModuleSource(preludeName);
            var prelude = new PreludeScript(preludeSource.Item1, preludeSource.Item2, getModuleSource, cancelCallbackFactory, logger);
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

            [DataMember] public bool isJson;

            [DataMember] public string body;

            [DataMember] public Dictionary<string, JRaw> metadata;

            public ExtraMetaData GetExtraMetadata()
            {
                if (metadata == null)
                    return null;
                return new ExtraMetaData(metadata);
            }
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
                _emittedEvents = new List<EmittedEventEnvelope>();
            _emittedEvents.Add(
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        emittedEvent.streamId, Guid.NewGuid(), emittedEvent.eventName, emittedEvent.isJson, emittedEvent.body,
                        emittedEvent.GetExtraMetadata(), _eventPosition, expectedTag: null)));
        }

        private QuerySourcesDefinition GetQuerySourcesDefinition()
        {
            CheckDisposed();
            var sourcesDefinition = _query.GetSourcesDefintion();
            if (sourcesDefinition == null)
                throw new InvalidOperationException("Invalid query.  No source definition.");
            return sourcesDefinition;
        }

        public void Load(string state)
        {
            CheckDisposed();
            _query.SetState(state);
        }

        public void LoadShared(string state)
        {
            CheckDisposed();
            _query.SetSharedState(state);
        }

        public void Initialize()
        {
            CheckDisposed();
            _query.Initialize();
        }

        public string GetStatePartition(
            CheckpointTag eventPosition, string category, ResolvedEvent @event)
        {
            CheckDisposed();
            if (@event == null) throw new ArgumentNullException("event");
            var partition = _query.GetPartition(
                @event.Data.Trim(), // trimming data passed to a JS 
                new string[]
                {
                    @event.EventStreamId, @event.IsJson ? "1" : "", @event.EventType, category ?? "",
                    @event.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), @event.Metadata ?? ""
                });
            if (partition == "")
                return null;
            else 
                return partition;
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category, ResolvedEvent data, out string newState,
            out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            CheckDisposed();
            if (data == null)
                throw new ArgumentNullException("data");
            _eventPosition = eventPosition;
            _emittedEvents = null;
            var newStates = _query.Push(
                data.Data.Trim(), // trimming data passed to a JS 
                new[]
                    {
                        data.IsJson ? "1" : "",
                        data.EventStreamId, data.EventType, category ?? "", data.EventSequenceNumber.ToString(CultureInfo.InvariantCulture),
                        data.Metadata, partition
                    });
            newState = newStates.Item1;
            newSharedState = newStates.Item2;
            try
            {
                if (!string.IsNullOrEmpty(newState))
                {
                    var jo = newState.ParseJson<JObject>();
                }

            }
            catch (JsonException)
            {
                Console.Error.WriteLine(newState);
            }
            emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
            return true;
        }

        public string TransformStateToResult()
        {
            CheckDisposed();
            var result = _query.TransformStateToResult();
            return result;
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

        public IQuerySources GetSourceDefinition()
        {
            return GetQuerySourcesDefinition();
        }
    }
}
