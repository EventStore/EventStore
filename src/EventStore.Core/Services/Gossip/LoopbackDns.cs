// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Net;

namespace EventStore.Core.Services.Gossip;

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
