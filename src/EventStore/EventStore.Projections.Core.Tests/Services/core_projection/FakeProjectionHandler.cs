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
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    public class FakeProjectionStateHandler : IProjectionStateHandler
    {
        public int _initializeCalled = 0;
        public int _loadCalled = 0;
        public int _eventsProcessed = 0;
        public string _loadedState = null;
        public string _lastProcessedStreamId;
        public string _lastProcessedEventType;
        public Guid _lastProcessedEventId;
        public int _lastProcessedSequencenumber;
        public string _lastProcessedMetadata;
        public string _lastProcessedData;
        public string _lastPartition;
        public const string _emit1Data = @"{""emit"":1}";
        public const string _emit2Data = @"{""emit"":2}";
        public const string _emit3Data = @"{""emit"":3}";
        public const string _emit1StreamId = "/emit1";
        public const string _emit2StreamId = "/emit2";
        public const string _emit1EventType = "emit1_event_type";
        public const string _emit2EventType = "emit2_event_type";

        private readonly bool _failOnInitialize;
        private readonly bool _failOnLoad;
        private readonly bool _failOnProcessEvent;
        private readonly bool _failOnGetPartition;
        private readonly Action<QuerySourceProcessingStrategyBuilder> _configureBuilder;

        public FakeProjectionStateHandler(
            bool failOnInitialize = false, bool failOnLoad = false, bool failOnProcessEvent = false,
            bool failOnGetPartition = true,
            Action<QuerySourceProcessingStrategyBuilder> configureBuilder = null)
        {
            _failOnInitialize = failOnInitialize;
            _failOnLoad = failOnLoad;
            _failOnProcessEvent = failOnProcessEvent;
            _failOnGetPartition = failOnGetPartition;
            _configureBuilder = configureBuilder;
        }

        public void ConfigureSourceProcessingStrategy(QuerySourceProcessingStrategyBuilder builder)
        {
            if (_configureBuilder != null)
                _configureBuilder(builder);
            else
            {
                builder.FromAll();
                builder.AllEvents();
                builder.SetDefinesStateTransform();
            }
        }

        public void Load(string state)
        {
            if (_failOnLoad)
                throw new Exception("LOAD_FAILED");
            _loadCalled++;
            _loadedState = state;
        }

        public void Initialize()
        {
            if (_failOnInitialize)
                throw new Exception("INITIALIZE_FAILED");
            _initializeCalled++;
            _loadedState = "";
        }

        public string GetStatePartition(
            CheckpointTag position, string streamId, string eventType, string category, Guid eventid, int sequenceNumber,
            string metadata, string data)
        {
            if (_failOnGetPartition)
                throw new Exception("GetStatePartition FAILED");
            return "region-a";
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string streamId, string eventType, string category, Guid eventId, int sequenceNumber,
            string metadata, string data, out string newState, out EmittedEvent[] emittedEvents)
        {
            if (_failOnProcessEvent)
                throw new Exception("PROCESS_EVENT_FAILED");
            _lastProcessedStreamId = streamId;
            _lastProcessedEventType = eventType;
            _lastProcessedEventId = eventId;
            _lastProcessedSequencenumber = sequenceNumber;
            _lastProcessedMetadata = metadata;
            _lastProcessedData = data;
            _lastPartition = partition;

            _eventsProcessed++;
            switch (eventType)
            {
                case "skip_this_type":
                    newState = null;
                    emittedEvents = null;
                    return false;
                case "handle_this_type":
                    _loadedState = newState = data;
                    emittedEvents = null;
                    return true;
                case "append":
                    _loadedState = newState = _loadedState + data;
                    emittedEvents = null;
                    return true;
                case "no_state_emit1_type":
                    _loadedState = newState = "";
                    emittedEvents = new[] { new EmittedDataEvent(_emit1StreamId, Guid.NewGuid(), _emit1EventType, _emit1Data, eventPosition, null), };
                    return true;
                case "emit1_type":
                    _loadedState = newState = data;
                    emittedEvents = new[] { new EmittedDataEvent(_emit1StreamId, Guid.NewGuid(), _emit1EventType, _emit1Data, eventPosition, null), };
                    return true;
                case "emit22_type":
                    _loadedState = newState = data;
                    emittedEvents = new[]
                        {
                            new EmittedDataEvent(_emit2StreamId, Guid.NewGuid(), _emit2EventType, _emit1Data, eventPosition, null),
                            new EmittedDataEvent(_emit2StreamId, Guid.NewGuid(), _emit2EventType, _emit2Data, eventPosition, null),
                        };
                    return true;
                case "emit212_type":
                    _loadedState = newState = data;
                    emittedEvents = new[]
                        {
                            new EmittedDataEvent(_emit2StreamId, Guid.NewGuid(), _emit2EventType, _emit1Data, eventPosition, null),
                            new EmittedDataEvent(_emit1StreamId, Guid.NewGuid(), _emit1EventType, _emit2Data, eventPosition, null),
                            new EmittedDataEvent(_emit2StreamId, Guid.NewGuid(), _emit2EventType, _emit3Data, eventPosition, null),
                        };
                    return true;
                case "emit12_type":
                    _loadedState = newState = data;
                    emittedEvents = new[]
                        {
                            new EmittedDataEvent(_emit1StreamId, Guid.NewGuid(), _emit1EventType, _emit1Data, eventPosition, null),
                            new EmittedDataEvent(_emit2StreamId, Guid.NewGuid(), _emit2EventType, _emit2Data, eventPosition, null),
                        };
                    return true;
                default:
                    throw new NotSupportedException();
            }
        }

        public string TransformStateToResult()
        {
            return _loadedState;
        }

        public void Dispose()
        {
        }
    }
}
