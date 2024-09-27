// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;

namespace EventStore.Core.Services.Gossip {
	public class KnownEndpointGossipSeedSource : IGossipSeedSource {
		private readonly EndPoint[] _ipEndPoints;

		public KnownEndpointGossipSeedSource(EndPoint[] ipEndPoints) {
			if (ipEndPoints == null)
				throw new ArgumentNullException("ipEndPoints");
			_ipEndPoints = ipEndPoints;
		}

		public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
			requestCallback(null);
			return null;
		}

		public EndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
			return _ipEndPoints;
		}
	}
}
