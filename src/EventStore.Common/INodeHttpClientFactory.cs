// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net.Http;

namespace EventStore.Common;

public interface INodeHttpClientFactory {
	HttpClient CreateHttpClient(string[] additionalCertificateNames);
}
