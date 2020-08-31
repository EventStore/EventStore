using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging {
	public sealed class RequestResponseDispatcher<TRequest, TResponse> : IHandle<TResponse>
		where TRequest : Message where TResponse : Message {
		//NOTE: this class is not intended to be used from multiple threads except from the QueuedHandlerThreadPool
		//however it supports count requests from other threads for statistics purposes
		private readonly Dictionary<Guid, Action<TResponse>> _map = new Dictionary<Guid, Action<TResponse>>();
		private readonly IPublisher _publisher;
		private readonly Func<TRequest, Guid> _getRequestCorrelationId;
		private readonly Func<TResponse, Guid> _getResponseCorrelationId;
		private readonly IEnvelope _defaultReplyEnvelope;
		private readonly Func<Guid, Message> _cancelMessageFactory;

		public RequestResponseDispatcher(
			IPublisher publisher,
			Func<TRequest, Guid> getRequestCorrelationId,
			Func<TResponse, Guid> getResponseCorrelationId,
			IEnvelope defaultReplyEnvelope,
			Func<Guid, Message> cancelMessageFactory = null) {
			_publisher = publisher;
			_getRequestCorrelationId = getRequestCorrelationId;
			_getResponseCorrelationId = getResponseCorrelationId;
			_defaultReplyEnvelope = defaultReplyEnvelope;
			_cancelMessageFactory = cancelMessageFactory;
		}

		public Guid Publish(TRequest request, Action<TResponse> action) {
			//TODO: expiration?
			Guid requestCorrelationId;
			lock (_map) {
				requestCorrelationId = _getRequestCorrelationId(request);
				_map.Add(requestCorrelationId, action);
			}

			_publisher.Publish(request);
			//NOTE: the following condition is required as publishing the message could also process the message 
			// and the correlationId is already invalid here
			lock (_map)
				return _map.ContainsKey(requestCorrelationId) ? requestCorrelationId : Guid.Empty;
		}

		void IHandle<TResponse>.Handle(TResponse message) {
			Handle(message);
		}

		public bool Handle(TResponse message) {
			var correlationId = _getResponseCorrelationId(message);
			bool handlerExists;
			Action<TResponse> action;

			lock (_map) {
				handlerExists = _map.TryGetValue(correlationId, out action);
				if (handlerExists) {
					_map.Remove(correlationId);
				}
			}

			// We mustn't call the handler inside the lock as we have no
			// knowledge of it's behaviour, which might result in dead locks.
			if (handlerExists) {
				action(message);
				return true;
			}

			return false;
		}

		public IEnvelope Envelope {
			get { return _defaultReplyEnvelope; }
		}

		public void Cancel(Guid requestId) {
			if (_cancelMessageFactory != null)
				_publisher.Publish(_cancelMessageFactory(requestId));
			lock (_map)
				_map.Remove(requestId);
		}

		public void CancelAll() {
			Guid[] ids = null;
			lock (_map) {
				_map.Clear();
				if (_cancelMessageFactory != null)
					ids = _map.Keys.ToArray();
			}

			if (ids != null)
				foreach (var id in ids)
					_publisher.Publish(_cancelMessageFactory(id));
		}
	}
}
