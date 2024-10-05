// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net.Http;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Client;

public class ClientOperationState {
	public readonly HttpRequestMessage Request;
	public readonly Action<HttpResponse> OnSuccess;
	public readonly Action<Exception> OnError;

	public HttpResponse Response { get; set; }

	public ClientOperationState(HttpRequestMessage request, Action<HttpResponse> onSuccess,
		Action<Exception> onError) {
		Ensure.NotNull(request, "request");
		Ensure.NotNull(onSuccess, "onSuccess");
		Ensure.NotNull(onError, "onError");

		Request = request;
		OnSuccess = onSuccess;
		OnError = onError;
	}

	public void Dispose() {
		Request.Dispose();
	}
}
