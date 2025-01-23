// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Security.Claims;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Common.Utils;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Tests.Http;

public class TestController(IPublisher publisher) : CommunicationController(publisher) {
	protected override void SubscribeCore(IUriRouter router) {
		Register(router, "/test1", Test1Handler);
		Register(router, "/test-anonymous", TestAnonymousHandler);
		Register(router, "/test-encoding/{a}?b={b}", TestEncodingHandler);
		Register(router, "/test-encoding-reserved-%20?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%20"));
		Register(router, "/test-encoding-reserved-%24?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%24"));
		Register(router, "/test-encoding-reserved-%25?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%25"));
		Register(router, "/test-encoding-reserved- ?b={b}", (manager, match) => TestEncodingHandler(manager, match, " "));
		Register(router, "/test-encoding-reserved-$?b={b}", (manager, match) => TestEncodingHandler(manager, match, "$"));
		Register(router, "/test-encoding-reserved-%?b={b}", (manager, match) => TestEncodingHandler(manager, match, "%"));
		Register(router, "/test-timeout?sleepfor={sleepfor}", (manager, match) => TestTimeoutHandler(manager, match));
	}

	private static void Register(
		IUriRouter router, string uriTemplate, Action<HttpEntityManager, UriTemplateMatch> handler,
		string httpMethod = HttpMethod.Get) {
		Register(router, uriTemplate, httpMethod, handler, Codec.NoCodecs, [Codec.ManualEncoding], new Operation(Operations.Node.StaticContent));
	}

	private static void Test1Handler(HttpEntityManager http, UriTemplateMatch match) {
		if (http.User != null && !http.User.HasClaim(ClaimTypes.Anonymous, ""))
			Reply(http, "OK", 200, "OK", "text/plain");
		else
			Reply(http, "Please authenticate yourself", 401, "Unauthorized", "text/plain");
	}

	private static void TestAnonymousHandler(HttpEntityManager http, UriTemplateMatch match) {
		if (!http.User.HasClaim(ClaimTypes.Anonymous, ""))
			Reply(http, "ERROR", 500, "ERROR", "text/plain");
		else
			Reply(http, "OK", 200, "OK", "text/plain");
	}

	private static void TestEncodingHandler(HttpEntityManager http, UriTemplateMatch match) {
		var a = match.BoundVariables["a"];
		var b = match.BoundVariables["b"];

		Reply(http, new {a = a, b = b, rawSegment = http.RequestedUrl.Segments[2]}.ToJson(), 200, "OK", "application/json");
	}

	private void TestEncodingHandler(HttpEntityManager http, UriTemplateMatch match, string a) {
		var b = match.BoundVariables["b"];

		Reply(
			http,
			new {
				a = a,
				b = b,
				rawSegment = http.RequestedUrl.Segments[1],
				requestUri = match.RequestUri,
				rawUrl = http.HttpEntity.Request.RawUrl
			}.ToJson(), 200, "OK", "application/json");
	}

	private static void TestTimeoutHandler(HttpEntityManager http, UriTemplateMatch match) {
		var sleepFor = int.Parse(match.BoundVariables["sleepfor"]);
		System.Threading.Thread.Sleep(sleepFor);
		Reply(http, "OK", 200, "OK", "text/plain");
	}

	public static void Reply(
		HttpEntityManager http, string response, int code, string description, string contentType,
		IEnumerable<KeyValuePair<string, string>> headers = null) {
		http.Reply(Helper.UTF8NoBom.GetBytes(response), code, description, contentType, Helper.UTF8NoBom, headers,
			exception => { });
	}
}
