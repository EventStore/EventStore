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

namespace EventStore.Projections.Core.Services.Processing
{
    class WriteQueryResultProjectionProcessingPhase : IProjectionProcessingPhase
    {
        public void Dispose()
        {
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            throw new NotImplementedException();
        }

        public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
        {
            throw new NotImplementedException();
        }

        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
            throw new NotImplementedException();
        }

        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            throw new NotImplementedException();
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            throw new NotImplementedException();
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            throw new NotImplementedException();
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            throw new NotImplementedException();
        }

        public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
        {
            throw new NotImplementedException();
        }

        public void ProcessEvent()
        {
            throw new NotImplementedException();
        }

        public void Subscribed(Guid subscriptionId)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribed()
        {
            throw new NotImplementedException();
        }

        public void SetState(PhaseState state)
        {
            throw new NotImplementedException();
        }

        public void SetFaulted()
        {
            throw new NotImplementedException();
        }

        public void GetStatistics(ProjectionStatistics info)
        {
            throw new NotImplementedException();
        }

        public IReaderStrategy ReaderStrategy
        {
            get { throw new NotImplementedException(); }
        }

        public ICoreProjectionCheckpointManager CheckpointManager
        {
            get { throw new NotImplementedException(); }
        }

        public ReaderSubscriptionOptions GetSubscriptionOptions()
        {
            throw new NotImplementedException();
        }
    }
}
