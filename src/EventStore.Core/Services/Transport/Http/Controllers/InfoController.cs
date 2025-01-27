// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json.Linq;
using static EventStore.Plugins.Authorization.Operations;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public static class InfoEndpoints {
	public static void MapGetInfo(this IEndpointRouteBuilder app) {
		app.MapGet("/info", ([FromServices] InfoController controller) => Results.Ok(
			new {
				ESVersion = VersionInfo.Version,
				State = controller.State.ToString().ToLower(),
				Features = controller.Features,
				Authentication = controller.GetAuthenticationInfo()
			}
		));
	}
}

public class InfoController(
	ClusterVNodeOptions options,
	IDictionary<string, bool> features,
	IAuthenticationProvider authenticationProvider)
	: IHttpController, IHandle<SystemMessage.StateChangeMessage> {
	private static readonly ILogger Log = Serilog.Log.ForContext<InfoController>();
	private static readonly ICodec[] SupportedCodecs = [Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text];

	private VNodeState _currentState;

	internal VNodeState State => _currentState;
	internal IDictionary<string, bool> Features => features;

	public void Subscribe(IUriRouter router) {
		Ensure.NotNull(router);

		// router.RegisterAction(new("/info", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Information.Read)), OnGetInfo);
		router.RegisterAction(new("/info/options", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Information.Options)), OnGetOptions);
	}

	public void Handle(SystemMessage.StateChangeMessage message) => _currentState = message.State;

	private void OnGetInfo(HttpEntityManager entity, UriTemplateMatch match) {
		entity.ReplyTextContent(
			Codec.Json.To(
				new {
					ESVersion = VersionInfo.Version,
					State = _currentState.ToString().ToLower(),
					Features = features,
					Authentication = GetAuthenticationInfo()
				}
			),
			HttpStatusCode.OK,
			"OK",
			entity.ResponseCodec.ContentType,
			null,
			e => Log.Error(e, "Error while writing HTTP response (info)"));
	}

	internal Dictionary<string, object> GetAuthenticationInfo() {
		if (authenticationProvider == null)
			return null;

		return new() {
			{ "type", authenticationProvider.Name },
			{ "properties", authenticationProvider.GetPublicProperties() }
		};
	}

	private void OnGetOptions(HttpEntityManager entity, UriTemplateMatch match) {
		if (entity.User != null && (entity.User.LegacyRoleCheck(SystemRoles.Operations) ||
		                            entity.User.LegacyRoleCheck(SystemRoles.Admins))) {
			var options1 = options.LoadedOptions.Values.Select(
				x => new OptionStructure {
					Name = x.Metadata.Name,
					Description = x.Metadata.Description,
					Group = x.Metadata.SectionMetadata.SectionType.Name,
					Value = x.DisplayValue,
					ConfigurationSource = x.SourceDisplayName,
					DeprecationMessage = x.Metadata.DeprecationMessage,
					Schema = x.Metadata.OptionSchema
				}
			);

			entity.ReplyTextContent(
				Codec.Json.To(options1),
				HttpStatusCode.OK,
				"OK",
				entity.ResponseCodec.ContentType,
				null,
				e => Log.Error(e, "error while writing HTTP response (options)")
			);
		} else {
			entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
		}
	}

	private static void LogReplyError(Exception exc) =>
		Log.Debug("Error while replying (info controller): {e}.", exc.Message);

	public class OptionStructure {
		public string Name { get; set; }
		public string Description { get; set; }
		public string Group { get; set; }
		public string Value { get; set; }
		public string ConfigurationSource { get; set; }
		public string DeprecationMessage { get; set; }
		public JObject Schema { get; set; }
	}
}
