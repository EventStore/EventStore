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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class EventDistributionPoint : IHandle<ClientMessage.ReadStreamEventsForwardCompleted>,
                                                   IHandle<ClientMessage.ReadAllEventsForwardCompleted>,
                                                   IDisposable
    {
        protected readonly Guid _distibutionPointCorrelationId;
        protected readonly IPublisher _publisher;
        protected readonly ILogger _logger = LogManager.GetLoggerFor<EventDistributionPoint>();

        protected bool _paused = true;
        protected bool _pauseRequested = true;
        protected bool _disposed;

        protected EventDistributionPoint(
            IPublisher publisher, Guid distibutionPointCorrelationId)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (distibutionPointCorrelationId == Guid.Empty)
                throw new ArgumentException("distibutionPointCorrelationId");
            _publisher = publisher;
            _distibutionPointCorrelationId = distibutionPointCorrelationId;
        }

        public void Resume()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (!_pauseRequested)
                throw new InvalidOperationException("Is not paused");
            if (!_paused)
            {
                _pauseRequested = false;
                return;
            }

            _paused = false;
            _pauseRequested = false;
            _logger.Trace("Resuming event distribution {0} at '{1}'", _distibutionPointCorrelationId, FromAsText());
            RequestEvents();
        }

        public void Pause()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested)
                throw new InvalidOperationException("Pause has been already requested");
            _pauseRequested = true;
            if (!AreEventsRequested())
                _paused = true;
            _logger.Trace("Pausing event distribution {0} at '{1}'", _distibutionPointCorrelationId, FromAsText());
        }

        public abstract void Handle(ClientMessage.ReadStreamEventsForwardCompleted message);
        public abstract void Handle(ClientMessage.ReadAllEventsForwardCompleted message);

        public void Dispose()
        {
            _disposed = true;
        }

        protected abstract bool AreEventsRequested();
        protected abstract string FromAsText();
        protected abstract void RequestEvents();

        protected ProjectionMessage.CoreService.Tick CreateTickMessage()
        {
            return
                new ProjectionMessage.CoreService.Tick(
                    () => { if (!_paused && !_disposed) RequestEvents(); });
        }
    }
}
