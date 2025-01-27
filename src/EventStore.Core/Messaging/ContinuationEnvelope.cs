// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;

namespace EventStore.Core.Messaging;

public class ChannelEnvelope(Channel<Message> channel) : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message {
		channel.Writer.TryWrite(message);
	}
}

public class ChannelWithCompletionEnvelope(ChannelWriter<Message> writer) : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message {
		writer.TryWrite(message);
		writer.Complete();
	}
}

public class ContinuationEnvelope(
	Func<Message, CancellationToken, Task> onMessage,
	SemaphoreSlim semaphore,
	CancellationToken cancellationToken) : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message {
		try {
			semaphore.Wait(cancellationToken);
			onMessage(message, cancellationToken).ContinueWith(_ => semaphore.Release(), cancellationToken);
		}
		catch (ObjectDisposedException) {}
		catch (OperationCanceledException) {}
	}
}
