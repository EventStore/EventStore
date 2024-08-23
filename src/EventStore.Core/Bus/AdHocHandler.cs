using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public sealed class AdHocHandler<T>(Func<T, CancellationToken, ValueTask> handle) : IAsyncHandle<T> where T : Message {
	public AdHocHandler(Action<T> handle) : this(handle.InvokeAsync) {
	}

	ValueTask IAsyncHandle<T>.HandleAsync(T message, CancellationToken token) => handle.Invoke(message, token);
}

public struct AdHocHandlerStruct<T> : IHandle<T>, IHandleTimeout where T : Message {
	private readonly Action<T> _handle;
	private readonly Action _timeout;

	public AdHocHandlerStruct(Action<T> handle, Action timeout) {
		Ensure.NotNull(handle, "handle");

		HandlesTimeout = timeout is not null;
		_handle = handle;
		_timeout = timeout.OrNoOp();
	}

	public bool HandlesTimeout { get; }

	public void Handle(T response) {
		_handle(response);
	}

	public void Timeout() {
		_timeout();
	}
}

file static class AsyncOverSyncHelpers {
	public static ValueTask InvokeAsync<T>(this Action<T> action, T message, CancellationToken token)
		where T : Message {
		var task = ValueTask.CompletedTask;
		try {
			action.Invoke(message);
		} catch (Exception e) {
			task = ValueTask.FromException(e);
		}

		return task;
	}
}
