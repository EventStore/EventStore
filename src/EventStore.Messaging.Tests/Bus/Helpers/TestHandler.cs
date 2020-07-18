using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class TestHandler<T> : IHandle<T> where T : Message {
		public readonly List<T> HandledMessages = new List<T>();

		public void Handle(T message) {
			HandledMessages.Add(message);
		}
	}
}
