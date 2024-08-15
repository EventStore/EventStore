using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DotNext.Runtime;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode;

/// <summary>
/// Builder syntax for constructing <see cref="VNodeFSM"/> in the code
/// </summary>
public sealed class VNodeFSMBuilder {
	private readonly ReadOnlyValueReference<VNodeState> _stateRef;
	private readonly Dictionary<Type, Action<VNodeState, Message>>[] _handlers;
	private readonly Action<VNodeState, Message>[] _defaultHandlers;

	public VNodeFSMBuilder(ReadOnlyValueReference<VNodeState> stateRef) {
		_stateRef = stateRef;

		var maxState = (int)Enum.GetValues<VNodeState>().Max();
		_handlers = new Dictionary<Type, Action<VNodeState, Message>>[maxState + 1];
		_defaultHandlers = new Action<VNodeState, Message>[maxState + 1];
	}

	public VNodeFSMBuilder(VNodeState state)
		: this(Constant(state)) {
	}

	private static ReadOnlyValueReference<VNodeState> Constant(VNodeState state) {
		var box = new StrongBox<VNodeState> { Value = state };
		return new(box, ref box.Value);
	}

	internal void AddHandler<TActualMessage>(VNodeState state, Action<VNodeState, Message> handler)
		where TActualMessage : Message {
		var stateHandlers = _handlers[(int)state] ??= new();

		if (!stateHandlers.TryAdd(typeof(TActualMessage), handler)) {
			throw new InvalidOperationException(
				$"Handler already defined for state {state} and message {typeof(TActualMessage).FullName}");
		}
	}

	internal void AddDefaultHandler(VNodeState state, Action<VNodeState, Message> handler) {
		ref var defaultHandler = ref _defaultHandlers[(int)state];
		if (defaultHandler is not null)
			throw new InvalidOperationException($"Default handler already defined for state {state}");

		defaultHandler = handler;
	}

	public VNodeFSMStatesDefinition InAnyState() => InStates(Enum.GetValues<VNodeState>().Distinct().ToArray());

	public VNodeFSMStatesDefinition InState(VNodeState state) => InStates(state);

	public VNodeFSMStatesDefinition InStates(params VNodeState[] states) {
		return new VNodeFSMStatesDefinition(this, states);
	}

	public VNodeFSMStatesDefinition InAllStatesExcept(params VNodeState[] states) {
		Ensure.Positive(states.Length, "states.Length");

		var s = Enum.GetValues<VNodeState>().Except(states).Distinct().ToArray();
		return new VNodeFSMStatesDefinition(this, s);
	}

	public VNodeFSM Build() => new(_stateRef, _handlers, _defaultHandlers);
}
