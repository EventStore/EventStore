// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;

namespace EventStore.Core.Services.Gossip {
	public interface IGossipSeedSource {
		IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state);
		EndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult);
	}
}
