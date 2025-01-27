// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace EventStore.Core.Services.Transport.Http;

public class InternalDispatcherEndpoint(IPublisher inputBus, MultiQueuedHandler requestsMultiHandler) : IHandle<HttpMessage.PurgeTimedOutRequests> {
	private static readonly ILogger Log = Serilog.Log.ForContext<InternalDispatcherEndpoint>();
	private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
	private readonly IEnvelope _publishEnvelope = inputBus;

	public void Handle(HttpMessage.PurgeTimedOutRequests message) {
		requestsMultiHandler.PublishToAll(message);

		inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval, _publishEnvelope, message));
	}

	public Task InvokeAsync(HttpContext context, RequestDelegate next) {
		if (context.IsGrpc() || context.Request.Path.StartsWithSegments("/ui")) return next(context);

		if (!InternalHttpHelper.TryGetInternalContext(context, out var manager, out var match, out var tcs)) {
			return next(context);
		}

		requestsMultiHandler.Publish(new AuthenticatedHttpRequestMessage(manager, match));
		return tcs.Task;
	}
}
