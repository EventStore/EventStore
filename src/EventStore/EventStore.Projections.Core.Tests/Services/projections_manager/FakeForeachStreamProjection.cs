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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public class FakeForeachStreamProjection : IProjectionStateHandler
    {
        private readonly string _query;
        private readonly Action<string> _logger;
        private string _state;

        public FakeForeachStreamProjection(string query, Action<string> logger)
        {
            _query = query;
            _logger = logger;
        }

        public void Dispose()
        {
        }

        public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder)
        {
            _logger("ConfigureSourceProcessingStrategy(" + builder + ")");
            builder.FromAll();
            builder.AllEvents();
            builder.SetByStream();
            builder.SetDefinesStateTransform();
        }

        public void Load(string state)
        {
            _logger("Load(" + state + ")");
            _state = state;
        }

        public void LoadShared(string state)
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
            _logger("Initialize");
            _state = "";
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            _logger("GetStatePartition(" + "..." + ")");
            return @data.EventStreamId;
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data,
            out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            newSharedState = null;
            if (data.EventType == "fail" || _query == "fail")
                throw new Exception("failed");
            _logger("ProcessEvent(" + "..." + ")");
            newState = "{\"data\": " + _state + data + "}";
            emittedEvents = null;
            return true;
        }

        public string TransformStateToResult()
        {
            return _state;
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }

    }
}
