using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class SendOverHttpProcessor : IHandle<HttpMessage.SendOverHttp> {
		private readonly Random _rnd;
		private readonly Dictionary<IPEndPoint, IPublisher> _httpBuses = new Dictionary<IPEndPoint, IPublisher>();
		private readonly RandomTestRunner _runner;
		private readonly double _lossProb;
		private readonly double _dupProb;
		private readonly int _maxDelay;

		public SendOverHttpProcessor(Random rnd, RandomTestRunner runner, double lossProb, double dupProb,
			int maxDelay) {
			if (rnd == null) throw new ArgumentNullException("rnd");
			if (runner == null) throw new ArgumentNullException("runner");
			if (lossProb < 0.0 || lossProb > 1.0) throw new ArgumentOutOfRangeException("lossProb");
			if (dupProb < 0.0 || dupProb > 1.0) throw new ArgumentOutOfRangeException("dupProb");
			if (maxDelay <= 0) throw new ArgumentOutOfRangeException("maxDelay");

			_rnd = rnd;
			_runner = runner;
			_lossProb = lossProb;
			_dupProb = dupProb;
			_maxDelay = maxDelay;
		}

		public void RegisterEndPoint(IPEndPoint endPoint, IPublisher bus) {
			_httpBuses.Add(endPoint, bus);
		}

		public void Handle(HttpMessage.SendOverHttp message) {
			if (_rnd.NextDouble() < _lossProb)
				return;

			if (ShouldSkipMessage(message))
				return;

			IPublisher publisher;
			if (!_httpBuses.TryGetValue(message.EndPoint, out publisher))
				throw new InvalidOperationException(string.Format("No HTTP bus subscribed for EndPoint: {0}.",
					message.EndPoint));

			_runner.Enqueue(message.EndPoint, message.Message, publisher, 1 + _rnd.Next(_maxDelay));

			if (_rnd.NextDouble() < _dupProb)
				_runner.Enqueue(message.EndPoint, message.Message, publisher, 1 + _rnd.Next(_maxDelay));
		}

		protected virtual bool ShouldSkipMessage(HttpMessage.SendOverHttp message) {
			return false;
		}
	}
}
