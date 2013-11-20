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

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CheckpointSuggestedWorkItem : CheckpointWorkItemBase
    {
        private readonly IProjectionPhaseCheckpointManager _projectionPhase;
        private readonly EventReaderSubscriptionMessage.CheckpointSuggested _message;
        private readonly ICoreProjectionCheckpointManager _checkpointManager;

        private bool _completed = false;
        private bool _completeRequested = false;

        public CheckpointSuggestedWorkItem(
            IProjectionPhaseCheckpointManager projectionPhase, EventReaderSubscriptionMessage.CheckpointSuggested message,
            ICoreProjectionCheckpointManager checkpointManager)
            : base() 
        {
            _projectionPhase = projectionPhase;
            _message = message;
            _checkpointManager = checkpointManager;
        }

        protected override void WriteOutput()
        {
            _projectionPhase.SetCurrentCheckpointSuggestedWorkItem(this);
            if (_checkpointManager.CheckpointSuggested(_message.CheckpointTag, _message.Progress))
            {
                _projectionPhase.SetCurrentCheckpointSuggestedWorkItem(null);
                _completed = true;
            }
            _projectionPhase.NewCheckpointStarted(_message.CheckpointTag);
            NextStage();
        }

        protected override void CompleteItem()
        {
            if (_completed)
                NextStage();
            else
                _completeRequested = true;
        }

        internal void CheckpointCompleted()
        {
            if (_completeRequested)
                NextStage();
            else
                _completed = true;
        }
    }
}
