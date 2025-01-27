// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Util;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class ClusterWebUiController(IPublisher publisher, NodeSubsystems[] enabledNodeSubsystems) : CommunicationController(publisher) {
	private static readonly ILogger Log = Serilog.Log.ForContext<ClusterWebUiController>();

	//private readonly MiniWeb _commonWeb;
	private readonly MiniWeb _clusterNodeWeb = new("/web");

	protected override void SubscribeCore(IUriRouter router) {
		_clusterNodeWeb.RegisterControllerActions(router);
		router.RegisterRedirectAction("/", "/ui/query");
		router.RegisterRedirectAction("/web", "/web/index.html");

		router.RegisterAction(new("/sys/subsystems", HttpMethod.Get, Codec.NoCodecs, [Codec.Json], new Operation(Operations.Node.Information.Subsystems)), OnListNodeSubsystems);
	}

	private void OnListNodeSubsystems(HttpEntityManager http, UriTemplateMatch match) {
		http.ReplyTextContent(
			Codec.Json.To(enabledNodeSubsystems),
			200,
			"OK",
			"application/json",
			null,
			ex => Log.Information(ex, "Failed to prepare main menu")
		);
	}
}
