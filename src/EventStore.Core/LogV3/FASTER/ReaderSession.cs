// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using FASTER.core;

namespace EventStore.Core.LogV3.FASTER;

internal class ReaderSession<TValue> : IDisposable {
	public ReaderSession(
		ClientSession<SpanByte, TValue, TValue, TValue, Context<TValue>, ReaderFunctions<TValue>> clientSession,
		Context<TValue> context) {
		ClientSession = clientSession;
		Context = context;
	}

	public ClientSession<SpanByte, TValue, TValue, TValue, Context<TValue>, ReaderFunctions<TValue>> ClientSession { get; }
	public Context<TValue> Context { get; }

	public void Dispose() {
		ClientSession?.Dispose();
	}
}

