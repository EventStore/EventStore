// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http;

public class UriToActionMatch(
	UriTemplateMatch templateMatch,
	ControllerAction controllerAction,
	Func<HttpEntityManager, UriTemplateMatch, RequestParams> requestHandler) {
	public readonly UriTemplateMatch TemplateMatch = templateMatch;
	public readonly ControllerAction ControllerAction = controllerAction;
	public readonly Func<HttpEntityManager, UriTemplateMatch, RequestParams> RequestHandler = requestHandler;
}
