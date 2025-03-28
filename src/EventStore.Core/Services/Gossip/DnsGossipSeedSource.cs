// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Gossip;

public class DnsGossipSeedSource : IGossipSeedSource {
	private readonly string _hostname;
	private readonly int _managerHttpPort;

	public DnsGossipSeedSource(string hostname, int managerHttpPort) {
		_hostname = hostname;
		_managerHttpPort = managerHttpPort;
	}

	public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
		return Dns.BeginGetHostAddresses(_hostname, requestCallback, state);
	}

	public EndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
		var addresses = Dns.EndGetHostAddresses(asyncResult);

		return addresses.Select(address => new IPEndPoint(address, _managerHttpPort).WithClusterDns(_hostname)).ToArray();
	}
}
