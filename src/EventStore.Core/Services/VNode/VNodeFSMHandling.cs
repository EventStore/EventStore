// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode;

public readonly ref struct VNodeFSMHandling<TMessage>
	where TMessage : Message {
	private readonly VNodeFSMStatesDefinition _stateDef;
	private readonly bool _defaultHandler;

	internal VNodeFSMHandling(VNodeFSMStatesDefinition stateDef, bool defaultHandler) {
		_stateDef = stateDef;
		_defaultHandler = defaultHandler;
	}

	public VNodeFSMStatesDefinition Do(Action<TMessage> handler)
		=> Do(handler.ToAsync());

	public VNodeFSMStatesDefinition Do(Func<TMessage, CancellationToken, ValueTask> handler) {
		if (_defaultHandler) {
			foreach (var state in _stateDef.States) {
				_stateDef.FSM.AddDefaultHandler(state, handler.InvokeWithDowncast);
			}
		} else {
			foreach (var state in _stateDef.States) {
				_stateDef.FSM.AddHandler(state, handler);
			}
		}

		return _stateDef;
	}

	public VNodeFSMStatesDefinition Ignore() {
		return Do(NoOp);

		static ValueTask NoOp(Message msg, CancellationToken token)
			=> token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;
	}

	public VNodeFSMStatesDefinition ForwardTo(IAsyncHandle<Message> publisher) {
		Ensure.NotNull(publisher, "publisher");
		return Do(publisher.HandleAsync);
	}
}

file static class DelegateHelpers {
	public static ValueTask InvokeWithDowncast<TMessage>(this Func<TMessage, CancellationToken, ValueTask> action,
		Message message, CancellationToken token)
		where TMessage : Message
		=> action.Invoke((TMessage)message, token);
}
