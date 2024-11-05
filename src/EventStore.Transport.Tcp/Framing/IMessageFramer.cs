// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Transport.Tcp.Framing;

public interface IMessageFramer {
	bool HasData { get; }
	IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data);
	void Reset();
}

/// <summary>
/// Encodes outgoing messages in frames and decodes incoming frames.
/// For decoding it uses an internal state, raising a registered
/// callback, once full message arrives
/// </summary>
public interface IMessageFramer<out TMessage> : IMessageFramer {
	void UnFrameData(IEnumerable<ArraySegment<byte>> data);
	void UnFrameData(ArraySegment<byte> data);
	void RegisterMessageArrivedCallback(Action<TMessage> handler);
}

public interface IAsyncMessageFramer<out TMessage> : IMessageFramer {
	ValueTask UnFrameData(IEnumerable<ArraySegment<byte>> data, CancellationToken token);
	ValueTask UnFrameData(ArraySegment<byte> data, CancellationToken token);
	void RegisterMessageArrivedCallback(Func<TMessage, CancellationToken, ValueTask> handler);
}
