using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public interface IAsyncHandle<in T> where T : Message {
	ValueTask HandleAsync(T message, CancellationToken token);
}

public interface IHandle<in T> : IAsyncHandle<T> where T : Message {
	void Handle(T message);

	ValueTask IAsyncHandle<T>.HandleAsync(T message, CancellationToken token) {
		var task = ValueTask.CompletedTask;
		try {
			Handle(message);
		} catch (Exception e) {
			task = ValueTask.FromException(e);
		}

		return task;
	}
}
