// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Transport.Http {
	public static class ProxyHeaders {
		public const string XForwardedPort = "X-Forwarded-Port";
		public const string XForwardedProto = "X-Forwarded-Proto";
		public const string XForwardedHost = "X-Forwarded-Host";
		public const string XForwardedPrefix = "X-Forwarded-Prefix";
	}

	public static class ProxyHeaderValues {
		public const string XForwardedProtoHttp = "http";
		public const string XForwardedProtoHttps = "https";
	}
}
