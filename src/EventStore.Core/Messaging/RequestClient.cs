// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging;

public static class RequestClient {
	public static Task<TResponse> RequestAsync<TRequest, TResponse>(IPublisher publisher, Func<IEnvelope, TRequest> getRequest, CancellationToken cancellationToken)
		where TRequest : Message where TResponse : Message {
		var envelope = new TcsEnvelope<TResponse>();
		var request = getRequest(envelope);
		publisher.Publish(request);
		return envelope.Task.WaitAsync(cancellationToken);
	}
}
