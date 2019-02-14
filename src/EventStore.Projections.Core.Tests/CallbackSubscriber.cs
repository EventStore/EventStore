using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Tests {
	public static class CallbackSubscriber {
		class Impl<T> : IHandle<T> where T : Message {
			private readonly Action<T> _callback;

			public Impl(Action<T> callback) {
				_callback = callback;
			}

			public void Handle(T message) {
				_callback(message);
			}
		}

		public static IHandle<T> Create<T>(Action<T> callback) where T : Message {
			return new Impl<T>(callback);
		}
	}
}
