// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Core.Settings;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http;

public static class UrlRouterExtensions {
	public static void RegisterCustomAction(this IUriRouter router, ControllerAction action, Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler) {
		router.RegisterAction(Ensure.NotNull(action), Ensure.NotNull(handler));
	}

	public static void RegisterAction(this IUriRouter router, ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler) {
		Ensure.NotNull(handler);
		router.RegisterAction(Ensure.NotNull(action), (man, match) => {
			handler(man, match);
			return new(ESConsts.HttpTimeout);
		});
	}

	public static void RegisterRedirectAction(this IUriRouter router, string fromUrl, string toUrl) {
		router.RegisterAction(
			new(fromUrl, HttpMethod.Get, Codec.NoCodecs, [Codec.ManualEncoding], new Operation(Operations.Node.Redirect)),
			(http, match) => http.ReplyTextContent("Moved", 302, "Found", "text/plain", [new("Location", new Uri(match.BaseUri, toUrl).AbsoluteUri)], Console.WriteLine));
	}

	public static void RegisterController(this IUriRouter router, IHttpController controller) {
		controller.Subscribe(router);
	}
}
