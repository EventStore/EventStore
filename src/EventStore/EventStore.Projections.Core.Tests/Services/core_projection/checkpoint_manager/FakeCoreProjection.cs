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

using System.Collections.Generic;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager
{
    public class FakeCoreProjection : ICoreProjection
    {
        public readonly List<ProjectionMessage.Projections.CommittedEventReceived> _committedEventReceivedMessages =
            new List<ProjectionMessage.Projections.CommittedEventReceived>();

        public readonly List<ProjectionMessage.Projections.CheckpointSuggested> _checkpointSuggestedMessages =
            new List<ProjectionMessage.Projections.CheckpointSuggested>();

        public readonly List<ProjectionMessage.Projections.CheckpointCompleted> _checkpointCompletedMessages =
            new List<ProjectionMessage.Projections.CheckpointCompleted>();

        public readonly List<ProjectionMessage.Projections.PauseRequested> _pauseRequestedMessages =
            new List<ProjectionMessage.Projections.PauseRequested>();

        public readonly List<ProjectionMessage.Projections.CheckpointLoaded> _checkpointLoadedMessages =
            new List<ProjectionMessage.Projections.CheckpointLoaded>();

        public void Handle(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            _committedEventReceivedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.CheckpointSuggested message)
        {
            _checkpointSuggestedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.CheckpointCompleted message)
        {
            _checkpointCompletedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.PauseRequested message)
        {
            _pauseRequestedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.CheckpointLoaded message)
        {
            _checkpointLoadedMessages.Add(message);
        }
    }
}
