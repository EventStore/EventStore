// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;

namespace EventStore.Core.Tests.Services.Transport.Http;

public class FakeController(int reqCount, IUriRouter router) : IHttpController {
	public static readonly ICodec[] SupportedCodecs = [Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text];

	public readonly List<Tuple<string, string>> BoundRoutes = new List<Tuple<string, string>>();
	public readonly CountdownEvent CountdownEvent = new(reqCount);

	public void Subscribe(IUriRouter router) {
		Register("/", HttpMethod.Get);
		Register("/ping", HttpMethod.Get);
		Register("/halt", HttpMethod.Get);
		Register("/shutdown", HttpMethod.Get);
		Register("/streams/{stream}", HttpMethod.Post);
		Register("/streams/{stream}", HttpMethod.Delete);
		Register("/streams/{stream}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/{event}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/{event}/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/{event}/backward/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/{event}/forward/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/{event}/data", HttpMethod.Get);
		Register("/streams/{stream}/{event}/metadata", HttpMethod.Get);
		Register("/streams/{stream}/metadata", HttpMethod.Post);
		Register("/streams/{stream}/metadata?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/metadata/{event}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/metadata/{event}/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/metadata/{event}/backward/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/metadata/{event}/forward/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/{stream}/metadata/data", HttpMethod.Get);
		Register("/streams/{stream}/metadata/metadata", HttpMethod.Get);
		Register("/streams/{stream}/metadata/{event}/data", HttpMethod.Get);
		Register("/streams/{stream}/metadata/{event}/metadata", HttpMethod.Get);
		Register("/streams/$all?embed={embed}", HttpMethod.Get);
		Register("/streams/$all/{position}/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/$all/{position}/backward/{count}?embed={embed}", HttpMethod.Get);
		Register("/streams/$all/{position}/forward/{count}?embed={embed}", HttpMethod.Get);
		Register("/projections", HttpMethod.Get);
		Register("/projections/any", HttpMethod.Get);
		Register("/projections/all-non-transient", HttpMethod.Get);
		Register("/projections/transient", HttpMethod.Get);
		Register("/projections/onetime", HttpMethod.Get);
		Register("/projections/continuous", HttpMethod.Get);
		Register("/projections/transient?name={name}&type={type}&enabled={enabled}", HttpMethod.Post);
		Register(
			"/projections/onetime?name={name}&type={type}&enabled={enabled}&checkpoints={checkpoints}&emit={emit}",
			HttpMethod.Post);
		Register("/projections/continuous?name={name}&type={type}&enabled={enabled}&emit={emit}", HttpMethod.Post);
		Register("/projection/{name}/query?config={config}", HttpMethod.Get);
		Register("/projection/{name}/query?type={type}&emit={emit}", HttpMethod.Put);
		Register("/projection/{name}", HttpMethod.Get);
		Register(
			"/projection/{name}?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}",
			HttpMethod.Delete);
		Register("/projection/{name}/statistics", HttpMethod.Get);
		Register("/projection/{name}/debug", HttpMethod.Get);
		Register("/projection/{name}/state?partition={partition}", HttpMethod.Get);
		Register("/projection/{name}/result?partition={partition}", HttpMethod.Get);
		Register("/projection/{name}/command/disable", HttpMethod.Post);
		Register("/projection/{name}/command/enable", HttpMethod.Post);
		Register("/projection/{name}/command/reset", HttpMethod.Post);
		Register("/stats", HttpMethod.Get);
		Register("/stats/{*statPath}", HttpMethod.Get);
		Register("/users/", HttpMethod.Get);
		Register("/users/{login}", HttpMethod.Get);
		Register("/users/", HttpMethod.Post);
		Register("/users/{login}", HttpMethod.Put);
		Register("/users/{login}", HttpMethod.Delete);
		Register("/users/{login}/command/enable", HttpMethod.Post);
		Register("/users/{login}/command/disable", HttpMethod.Post);
		Register("/users/{login}/command/reset-password", HttpMethod.Post);
		Register("/users/{login}/command/change-password", HttpMethod.Post);
	}

	private void Register(string route, string verb) {
		router.RegisterAction(
			new(route, verb, Codec.NoCodecs, SupportedCodecs, new Operation()),
			(_, _) => {
				CountdownEvent.Signal();
				return new RequestParams(TimeSpan.Zero);
			});

		var uriTemplate = new UriTemplate(route);
		var bound = uriTemplate.BindByPosition(new Uri("http://localhost:12345/"),
			Enumerable.Range(0,
					uriTemplate.PathSegmentVariableNames.Count +
					uriTemplate.QueryValueVariableNames.Count)
				.Select(x => "abacaba")
				.ToArray());
		BoundRoutes.Add(Tuple.Create(bound.AbsoluteUri, verb));
	}
}
