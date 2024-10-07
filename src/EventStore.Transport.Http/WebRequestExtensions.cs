// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;

namespace EventStore.Transport.Http;

public static class WebRequestExtensions {
	public static WebResponse EndGetResponseExtended(this WebRequest request, IAsyncResult asyncResult) {
		try {
			return request.EndGetResponse(asyncResult);
		} catch (WebException e) {
			if (e.Response != null)
				return e.Response; //for 404 and 500

			throw;
		}
	}
}
