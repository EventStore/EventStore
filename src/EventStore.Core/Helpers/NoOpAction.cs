// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Helpers;

public class NoOpAction {
	public static Action Instance { get; } = () => { };
}

public class NoOpAction<T> {
	public static Action<T> Instance { get; } = _ => { };
}

public static class ActionExtensions {
	public static Action OrNoOp(this Action self) => self ?? NoOpAction.Instance;
	public static Action<T> OrNoOp<T>(this Action<T> self) => self ?? NoOpAction<T>.Instance;
}
