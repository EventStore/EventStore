// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http;

public class UriToActionMatch {
	public readonly UriTemplateMatch TemplateMatch;
	public readonly ControllerAction ControllerAction;
	public readonly Func<HttpEntityManager, UriTemplateMatch, RequestParams> RequestHandler;

	public UriToActionMatch(UriTemplateMatch templateMatch,
		ControllerAction controllerAction,
		Func<HttpEntityManager, UriTemplateMatch, RequestParams> requestHandler) {
		TemplateMatch = templateMatch;
		ControllerAction = controllerAction;
		RequestHandler = requestHandler;
	}
}
