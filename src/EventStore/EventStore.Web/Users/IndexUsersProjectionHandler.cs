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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Web.Users
{
    public sealed class IndexUsersProjectionHandler : IProjectionStateHandler
    {
        private const string UserStreamPrefix = "$user-";
        private const string UsersStream = "$users";
        private const string UserEventType = "$User";

        public IndexUsersProjectionHandler(string query, Action<string> logger)
        {
        }

        public void Dispose()
        {
        }

        public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder)
        {
            builder.FromAll();
            builder.IncludeEvent("$UserCreated");
        }

        public void Load(string state)
        {
        }

        public void LoadShared(string state)
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category, ResolvedEvent data, out string newState,
            out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            newSharedState = null;
            if (!data.EventStreamId.StartsWith(UserStreamPrefix))
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid stream name: '{0}' The IndexUsersProjectionHandler cannot handle events from other streams than named after the '$user-' pattern",
                        data.EventStreamId));

            var loginName = data.EventStreamId.Substring(UserStreamPrefix.Length);

            var userData = data.Data.ParseJson<UserData>();
            if (userData.LoginName != loginName)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid $UserCreated event found.  '{0}' login name expected, but '{1}' found", loginName,
                        userData.LoginName));

            emittedEvents = new[]
            {
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        UsersStream, Guid.NewGuid(), UserEventType, false, loginName, null, eventPosition, null))
            };
            newState = "";
            return true;
        }

        public string TransformStateToResult()
        {
            throw new NotImplementedException();
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }

    }
}
