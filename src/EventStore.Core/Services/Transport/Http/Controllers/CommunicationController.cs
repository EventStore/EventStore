// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Microsoft.AspNetCore.Mvc;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public abstract class CommunicationController(IPublisher publisher) : IHttpController {
	private static readonly ILogger Log = Serilog.Log.ForContext<CommunicationController>();
	private static readonly ICodec[] DefaultCodecs = [Codec.Json, Codec.Xml];

	private readonly IPublisher _publisher = Ensure.NotNull(publisher);

	protected void Publish(Message message) => _publisher.Publish(Ensure.NotNull(message));

	public void Subscribe(IUriRouter router) => SubscribeCore(Ensure.NotNull(router));

	protected abstract void SubscribeCore(IUriRouter router);

	protected static RequestParams SendBadRequest(HttpEntityManager httpEntityManager, string reason) {
		httpEntityManager.ReplyContent(Encoding.ASCII.GetBytes(reason), HttpStatusCode.BadRequest,
			"Bad Request",type: "text/plain", headers:null,
			e => Log.Debug("Error while closing HTTP connection (bad request): {e}.", e.Message));

		return new(done: true);
	}

	protected static RequestParams SendTooBig(HttpEntityManager httpEntityManager) {
		httpEntityManager.ReplyStatus(HttpStatusCode.RequestEntityTooLarge,
			"Too large events received. Limit is 4mb",
			_ => Log.Debug("Too large events received over HTTP"));
		return new(done: true);
	}

	protected static RequestParams SendOk(HttpEntityManager httpEntityManager) {
		httpEntityManager.ReplyStatus(HttpStatusCode.OK,
			"OK",
			e => Log.Debug("Error while closing HTTP connection (ok): {e}.", e.Message));
		return new(done: true);
	}

	protected static void Register(IUriRouter router, string uriTemplate, string httpMethod,
		Action<HttpEntityManager, UriTemplateMatch> handler, ICodec[] requestCodecs, ICodec[] responseCodecs, Operation operation) {
		router.RegisterAction(new(uriTemplate, httpMethod, requestCodecs, responseCodecs, operation), handler);
	}

	protected static void Register(IUriRouter router, string uriTemplate, string httpMethod,
		Action<HttpEntityManager, UriTemplateMatch> handler, ICodec[] requestCodecs, ICodec[] responseCodecs, Func<UriTemplateMatch,Operation> operation) {
		router.RegisterAction(new(uriTemplate, httpMethod, requestCodecs, responseCodecs, operation), handler);
	}

	protected static void RegisterCustom(IUriRouter router, string uriTemplate, string httpMethod,
		Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler,
		ICodec[] requestCodecs, ICodec[] responseCodecs, Operation operation) {
		router.RegisterCustomAction(new(uriTemplate, httpMethod, requestCodecs, responseCodecs, operation), handler);
	}

	protected static void RegisterCustom(IUriRouter router, string uriTemplate, string httpMethod,
		Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler,
		ICodec[] requestCodecs, ICodec[] responseCodecs, Func<UriTemplateMatch,Operation> operation) {
		router.RegisterCustomAction(new(uriTemplate, httpMethod, requestCodecs, responseCodecs, operation), handler);
	}

	protected static void RegisterUrlBased(IUriRouter router, string uriTemplate, string httpMethod, Operation operation,
		Action<HttpEntityManager, UriTemplateMatch> action) {
		Register(router, uriTemplate, httpMethod, action, Codec.NoCodecs, DefaultCodecs, operation);
	}

	protected static void RegisterUrlBased(IUriRouter router, string uriTemplate, string httpMethod, Func<UriTemplateMatch,Operation> operation,
		Action<HttpEntityManager, UriTemplateMatch> action) {
		Register(router, uriTemplate, httpMethod, action, Codec.NoCodecs, DefaultCodecs, operation);
	}

	protected static string MakeUrl(HttpEntityManager http, string path) {
		if (path.Length > 0 && path[0] == '/') path = path.Substring(1);
		var hostUri = http.ResponseUrl;
		var builder = new UriBuilder(hostUri.Scheme, hostUri.Host, hostUri.Port, hostUri.LocalPath + path);
		return builder.Uri.AbsoluteUri;
	}
}
