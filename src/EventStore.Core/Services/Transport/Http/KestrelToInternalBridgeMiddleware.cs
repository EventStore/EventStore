// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Microsoft.AspNetCore.Http;
using Serilog;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services.Transport.Http;

public class KestrelToInternalBridgeMiddleware(IUriRouter uriRouter, bool logHttpRequests, string advertiseAsAddress, int advertiseAsPort) : IMiddleware {
	private static readonly ILogger Log = Serilog.Log.ForContext<KestrelToInternalBridgeMiddleware>();

	public Task InvokeAsync(HttpContext context, RequestDelegate next) {
		return TryMatch(context, uriRouter, logHttpRequests, advertiseAsAddress, advertiseAsPort) ? next(context) : Task.CompletedTask;
	}

	private static bool TryMatch(HttpContext context, IUriRouter uriRouter, bool logHttpRequests, string advertiseAsAddress, int advertiseAsPort) {
		if (context.IsGrpc() || context.Request.Path.StartsWithSegments("/ui")) {
			return true;
		}

		var tcs = new TaskCompletionSource<bool>();
		var httpEntity = new HttpEntity(context, logHttpRequests, advertiseAsAddress, advertiseAsPort, () => tcs.TrySetResult(true));

		var request = httpEntity.Request;
		try {
			var allMatches = uriRouter.GetAllUriMatches(request.Url);
			if (allMatches.Count == 0) {
				return true;
			}

			var allowedMethods = GetAllowedMethods(allMatches);

			if (request.HttpMethod.Equals(HttpMethod.Options, StringComparison.OrdinalIgnoreCase)) {
				RespondWithOptions(httpEntity, allowedMethods);
				return false;
			}

			var match = allMatches.LastOrDefault(m => m.ControllerAction.HttpMethod.Equals(request.HttpMethod, StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				MethodNotAllowed(httpEntity, allowedMethods);
				return false;
			}

			ICodec requestCodec = null;
			var supportedRequestCodecs = match.ControllerAction.SupportedRequestCodecs;
			if (supportedRequestCodecs is { Length: > 0 }) {
				requestCodec = SelectRequestCodec(request.HttpMethod, request.ContentType, supportedRequestCodecs);
				if (requestCodec == null) {
					BadContentType(httpEntity, "Invalid or missing Content-Type");
					return false;
				}
			}

			ICodec responseCodec = SelectResponseCodec(request,
				request.AcceptTypes,
				match.ControllerAction.SupportedResponseCodecs,
				match.ControllerAction.DefaultResponseCodec);
			if (responseCodec == null) {
				BadCodec(httpEntity, "Requested URI is not available in requested format");
				return false;
			}

			try {
				var manager = httpEntity.CreateManager(requestCodec, responseCodec, allowedMethods, satisfied => { });
				context.Items.Add(manager.GetType(), manager);
				context.Items.Add(match.GetType(), match);
				context.Items.Add(tcs.GetType(), tcs);
				return true;
			} catch (Exception exc) {
				Log.Error(exc, "Error while handling HTTP request '{url}'.", request.Url);
				InternalServerError(httpEntity);
			}
		} catch (Exception exc) {
			Log.Error(exc, "Unhandled exception while processing HTTP request at {url}.", httpEntity.RequestedUrl);
			InternalServerError(httpEntity);
		}

		return false;
	}

	private static string[] GetAllowedMethods(List<UriToActionMatch> allMatches) {
		var allowedMethods = new string[allMatches.Count + 1];
		for (int i = 0; i < allMatches.Count; ++i) {
			allowedMethods[i] = allMatches[i].ControllerAction.HttpMethod;
		}

		//add options to the list of allowed request methods
		allowedMethods[allMatches.Count] = HttpMethod.Options;
		return allowedMethods;
	}

	private static void RespondWithOptions(HttpEntity httpEntity, string[] allowed) {
		var entity = httpEntity.CreateManager(Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
		entity.ReplyStatus(HttpStatusCode.OK, "OK", e => Log.Debug("Error while closing HTTP connection (http service core): {e}.", e.Message));
	}

	private static void MethodNotAllowed(HttpEntity httpEntity, string[] allowed) {
		var entity = httpEntity.CreateManager(Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
		entity.ReplyStatus(HttpStatusCode.MethodNotAllowed, "Method Not Allowed",
			e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
	}

	private static void NotFound(HttpEntity httpEntity) {
		var entity = httpEntity.CreateManager();
		entity.ReplyStatus(HttpStatusCode.NotFound, "Not Found", e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
	}

	private static void InternalServerError(HttpEntity httpEntity) {
		var entity = httpEntity.CreateManager();
		entity.ReplyStatus(HttpStatusCode.InternalServerError, "Internal Server Error",
			e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
	}

	private static void BadCodec(HttpEntity httpEntity, string reason) {
		var entity = httpEntity.CreateManager();
		entity.ReplyStatus(HttpStatusCode.NotAcceptable, reason,
			e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
	}

	private static void BadContentType(HttpEntity httpEntity, string reason) {
		var entity = httpEntity.CreateManager();
		entity.ReplyStatus(HttpStatusCode.UnsupportedMediaType, reason,
			e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
	}

	private static ICodec SelectRequestCodec(string method, string contentType, ICodec[] supportedCodecs) {
		if (string.IsNullOrEmpty(contentType))
			return supportedCodecs != null && supportedCodecs.Length > 0 ? null : Codec.NoCodec;
		return method.ToUpper() switch {
			HttpMethod.Post or HttpMethod.Put or HttpMethod.Delete => supportedCodecs.SingleOrDefault(c => c.CanParse(MediaType.Parse(contentType))),
			_ => Codec.NoCodec
		};
	}

	private static ICodec SelectResponseCodec(IHttpRequest query, string[] acceptTypes, ICodec[] supported, ICodec @default) {
		var requestedFormat = GetFormatOrDefault(query);
		if (requestedFormat == null && acceptTypes.IsEmpty())
			return @default;

		if (requestedFormat != null)
			return supported.FirstOrDefault(c => c.SuitableForResponse(MediaType.Parse(requestedFormat)));

		return acceptTypes.Select(MediaType.TryParse)
			.Where(x => x != null)
			.OrderByDescending(v => v.Priority)
			.Select(type => supported.FirstOrDefault(codec => codec.SuitableForResponse(type)))
			.FirstOrDefault(corresponding => corresponding != null);
	}

	private static string GetFormatOrDefault(IHttpRequest request) {
		var format = request.GetQueryStringValues("format").FirstOrDefault()?.ToLower();

		return format switch {
			null or "" => null,
			"json" => ContentType.Json,
			"text" => ContentType.PlainText,
			"xml" => ContentType.Xml,
			"atom" => ContentType.Atom,
			"atomxj" => ContentType.AtomJson,
			"atomsvc" => ContentType.AtomServiceDoc,
			"atomsvcxj" => ContentType.AtomServiceDocJson,
			_ => throw new NotSupportedException("Unknown format requested")
		};
	}
}
