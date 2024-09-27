// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Linq;
using System.Net.Http.Headers;

namespace EventStore.Core.Tests.Http {
	internal static class HttpResponseHeaderExtensions {
		public static string GetLocationAsString(this HttpResponseHeaders headers)
			=> headers.TryGetValues("location", out var values)
				? values.FirstOrDefault()
				: default;

	}
}
