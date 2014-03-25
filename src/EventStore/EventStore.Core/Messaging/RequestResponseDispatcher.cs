using System;
using System.Collections.Generic;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging
{
    public sealed class RequestResponseDispatcher<TRequest, TResponse> : IHandle<TResponse>
        where TRequest : Message where TResponse : Message
    {
        //NOTE: this class is not intended to be used from multiple threads except from the QueuedHandlerThreadPool
        //however it supports count requests from other threads for statistics purposes
        private readonly Dictionary<Guid, Action<TResponse>> _map = new Dictionary<Guid, Action<TResponse>>();
        private readonly IPublisher _publisher;
        private readonly Func<TRequest, Guid> _getRequestCorrelationId;
        private readonly Func<TResponse, Guid> _getResponseCorrelationId;
        private readonly IEnvelope _defaultReplyEnvelope;

        public RequestResponseDispatcher(
            IPublisher publisher, Func<TRequest, Guid> getRequestCorrelationId,
            Func<TResponse, Guid> getResponseCorrelationId, IEnvelope defaultReplyEnvelope)
        {
            _publisher = publisher;
            _getRequestCorrelationId = getRequestCorrelationId;
            _getResponseCorrelationId = getResponseCorrelationId;
            _defaultReplyEnvelope = defaultReplyEnvelope;
        }

        public Guid Publish(TRequest request, Action<TResponse> action)
        {
            //TODO: expiration?
            Guid requestCorrelationId;
            lock (_map)
            {
                requestCorrelationId = _getRequestCorrelationId(request);
                _map.Add(requestCorrelationId, action);
            }
            _publisher.Publish(request);
            //NOTE: the following condition is required as publishing the message could also process the message 
            // and the correlationId is already invalid here
            lock (_map)
                return _map.ContainsKey(requestCorrelationId) ? requestCorrelationId : Guid.Empty;
        }

        void IHandle<TResponse>.Handle(TResponse message)
        {
            Handle(message);
        }

        public bool Handle(TResponse message)
        {
            var correlationId = _getResponseCorrelationId(message);
            Action<TResponse> action;
            lock (_map)
                if (_map.TryGetValue(correlationId, out action))
                {
                    _map.Remove(correlationId);
                    action(message);
                    return true;
                }
            return false;
        }

        public IEnvelope Envelope
        {
            get { return _defaultReplyEnvelope; }
        }

        public void Cancel(Guid requestId)
        {
            lock (_map)
                _map.Remove(requestId);
        }

        public void CancelAll()
        {
            lock (_map)
                _map.Clear();
        }
    }
}
