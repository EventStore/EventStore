// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using static EventStore.Plugins.Authorization.Operations.Node;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public static class StatsEndpoints {
	public static void MapGetStats(this IEndpointRouteBuilder app) {
		app.MapGet("/stats1/{*statPath}", async ([CanBeNull] string statPath, HttpRequest request, [FromKeyedServices("monitoring")] IPublisher publisher, CancellationToken cancellationToken) => {
			var metadata = request.Query.TryGetValue("metadata", out var metadataStr) && bool.Parse(metadataStr);
			var group = request.Query.TryGetValue("group", out var groupStr) && bool.Parse(groupStr);
			Log.Debug("Stats {Path} {Metadata} {Group}", statPath, metadata, group);
			if (!group && !string.IsNullOrEmpty(statPath)) {
				return Results.BadRequest("Dynamic stats selection works only with grouping enabled");
			}

			var statSelector = StatController.GetStatSelector(statPath);

			var resp = await RequestClient.RequestAsync<MonitoringMessage.GetFreshStats, MonitoringMessage.GetFreshStatsCompleted>(
				publisher,
				e => new(e, statSelector, metadata, group),
				cancellationToken
			);
			return Results.Ok(resp.Stats);
		}).RequireAuthorization();
	}
}

public class StatController(IPublisher publisher, IPublisher networkSendQueue) : CommunicationController(publisher) {
	private static readonly ICodec[] SupportedCodecs = [Codec.Json, Codec.Xml, Codec.ApplicationXml];

	protected override void SubscribeCore(IUriRouter router) {
		Ensure.NotNull(router);

		router.RegisterAction(new("/stats", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Statistics.Read)), OnGetFreshStats);
		router.RegisterAction(new("/stats/replication", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Statistics.Replication)), OnGetReplicationStats);
		router.RegisterAction(new("/stats/tcp", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Statistics.Tcp)), OnGetTcpConnectionStats);
		router.RegisterAction(new("/stats/{*statPath}", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Statistics.Custom)), OnGetFreshStats);
	}

	private void OnGetTcpConnectionStats(HttpEntityManager entity, UriTemplateMatch match) {
		var envelope = new SendToHttpEnvelope(networkSendQueue,
			entity,
			Format.GetFreshTcpConnectionStatsCompleted,
			Configure.GetFreshTcpConnectionStatsCompleted);
		Publish(new MonitoringMessage.GetFreshTcpConnectionStats(envelope));
	}

	private void OnGetFreshStats(HttpEntityManager entity, UriTemplateMatch match) {
		var envelope = new SendToHttpEnvelope(networkSendQueue,
			entity,
			Format.GetFreshStatsCompleted,
			Configure.GetFreshStatsCompleted);

		var statPath = match.BoundVariables["statPath"];
		var statSelector = GetStatSelector(statPath);

		if (!bool.TryParse(match.QueryParameters["metadata"], out var useMetadata))
			useMetadata = false;

		if (!bool.TryParse(match.QueryParameters["group"], out var useGrouping))
			useGrouping = true;

		if (!useGrouping && !string.IsNullOrEmpty(statPath)) {
			SendBadRequest(entity, "Dynamic stats selection works only with grouping enabled");
			return;
		}

		Publish(new MonitoringMessage.GetFreshStats(envelope, statSelector, useMetadata, useGrouping));
	}

	internal static Func<Dictionary<string, object>, Dictionary<string, object>> GetStatSelector(string statPath) {
		if (string.IsNullOrEmpty(statPath))
			return dict => dict;

		if (statPath.StartsWith("stats/")) {
			statPath = statPath[6..];
			if (string.IsNullOrEmpty(statPath))
				return dict => dict;
		}

		var groups = statPath.Split('/');

		return dict => {
			Ensure.NotNull(dict, "dictionary");

			foreach (string groupName in groups) {
				if (!dict.TryGetValue(groupName, out var item))
					return null;

				dict = item as Dictionary<string, object>;

				if (dict == null)
					return null;
			}

			return dict;
		};
	}

	private void OnGetReplicationStats(HttpEntityManager entity, UriTemplateMatch match) {
		var envelope = new SendToHttpEnvelope(networkSendQueue,
			entity,
			Format.GetReplicationStatsCompleted,
			Configure.GetReplicationStatsCompleted);
		Publish(new ReplicationMessage.GetReplicationStats(envelope));
	}
}
