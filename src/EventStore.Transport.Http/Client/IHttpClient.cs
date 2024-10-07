// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Transport.Http.Client;

public interface IHttpClient {
	void Get(string url, Action<HttpResponse> onSuccess, Action<Exception> onException);

	void Post(string url, string request, string contentType, Action<HttpResponse> onSuccess,
		Action<Exception> onException);

	void Delete(string url, Action<HttpResponse> onSuccess, Action<Exception> onException);

	void Put(string url, string request, string contentType, Action<HttpResponse> onSuccess,
		Action<Exception> onException);
}
