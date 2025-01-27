// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Messaging;

// WARNING: ReplyWith only _calls_ onMessage.
// - it does not await it because ReplyWith is not asynchronous
// - it does not synchronously wait for it because onMessage might be scheduled on our local
//      task queue and we'd have to wait until another thread comes and steals it
// - instead we just call it and return.
// - the synchronous portion of onMessage must be quick as it is called on the replyer's thread
// - the asynchronous portion of onMessage must be aware that the call to ReplyWith has completed and
//      therefore a single thread calling ReplyWith might call it again before onMessage has completed.
public class AsyncTaskEnvelope(Func<Message, CancellationToken, Task> onMessage, CancellationToken cancellationToken) : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message {
		try {
			onMessage(message, cancellationToken);
		} catch (OperationCanceledException) {
			// depending on the implementation of _onMessage, the OperationCancelled exception might be caught here or
			// it might end up on the Task according to whether it is thrown synchronously or not. this isn't a problem
			// as we're ignoring the exception in both cases.
		}
	}
}
