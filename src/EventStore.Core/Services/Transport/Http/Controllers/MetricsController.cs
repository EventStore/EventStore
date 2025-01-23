// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class MetricsController() : CommunicationController(new NoOpPublisher()) {
	private static readonly ICodec[] SupportedCodecs = [
		Codec.CreateCustom(Codec.Text, "text/plain", Helper.UTF8NoBom, false, false),
		Codec.CreateCustom(Codec.Text, "application/openmetrics-text", Helper.UTF8NoBom, false, false)
	];

	protected override void SubscribeCore(IUriRouter router) {
		Ensure.NotNull(router);

		// this exists only to specify the permissions required for the /metrics endpoint
		router.RegisterAction(new("/metrics", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs,
			new Operation(Operations.Node.Statistics.Read)),
			// the PrometheusExporterMiddleware handles the request itself, this will not be called
			(_, _) => throw new InvalidOperationException());
	}

	class NoOpPublisher : IPublisher {
		public void Publish(Message message) {
		}
	}
}
