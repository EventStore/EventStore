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
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.TimerService
{
    public static class TimerMessage
    {
        public class Schedule : Message
        {
            public readonly TimeSpan TriggerAfter;
            
            public readonly IEnvelope Envelope;
            public readonly Message ReplyMessage;

            private readonly Action _replyAction;

            public static Schedule Create<T>(TimeSpan triggerAfter, IEnvelope envelope, T replyMessage) where T : Message
            {
                return new Schedule(triggerAfter, envelope, replyMessage, () => envelope.ReplyWith(replyMessage));
            }

            private Schedule(TimeSpan triggerAfter, IEnvelope envelope, Message replyMessage, Action replyAction)
            {
                if (envelope == null)
                    throw new ArgumentNullException("envelope");
                if (replyMessage == null)
                    throw new ArgumentNullException("replyMessage");
                if (replyAction == null) 
                    throw new ArgumentNullException("replyAction");

                TriggerAfter = triggerAfter;
                Envelope = envelope;
                ReplyMessage = replyMessage;
                _replyAction = replyAction;
            }

            public void Reply()
            {
                _replyAction();
            }
        }
    }
}