// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data;

public class GossipAdvertiseInfo {
	public DnsEndPoint InternalTcp { get; }
	public DnsEndPoint InternalSecureTcp { get; }
	public DnsEndPoint ExternalTcp { get; }
	public DnsEndPoint ExternalSecureTcp { get; }
	public DnsEndPoint HttpEndPoint { get; }
	public string AdvertiseInternalHostAs { get; }
	public string AdvertiseExternalHostAs { get; }
	public int AdvertiseHttpPortAs { get; }
	public string AdvertiseHostToClientAs { get; }
	public int AdvertiseHttpPortToClientAs { get; }
	public int AdvertiseTcpPortToClientAs { get; }

	public GossipAdvertiseInfo(DnsEndPoint internalTcp, DnsEndPoint internalSecureTcp,
		DnsEndPoint externalTcp, DnsEndPoint externalSecureTcp,
		DnsEndPoint httpEndPoint,
		string advertiseInternalHostAs, string advertiseExternalHostAs, int advertiseHttpPortAs,
		string advertiseHostToClientAs, int advertiseHttpPortToClientAs, int advertiseTcpPortToClientAs) {
		Ensure.Equal(false, internalTcp == null && internalSecureTcp == null, "Both internal TCP endpoints are null");

		InternalTcp = internalTcp;
		InternalSecureTcp = internalSecureTcp;
		ExternalTcp = externalTcp;
		ExternalSecureTcp = externalSecureTcp;
		HttpEndPoint = httpEndPoint;
		AdvertiseInternalHostAs = advertiseInternalHostAs;
		AdvertiseExternalHostAs = advertiseExternalHostAs;
		AdvertiseHttpPortAs = advertiseHttpPortAs;
		AdvertiseHostToClientAs = advertiseHostToClientAs;
		AdvertiseHttpPortToClientAs = advertiseHttpPortToClientAs;
		AdvertiseTcpPortToClientAs = advertiseTcpPortToClientAs;
	}

	public override string ToString() {
		return string.Format(
			$"IntTcp: {InternalTcp}, IntSecureTcp: {InternalSecureTcp}\n" +
			$"ExtTcp: {ExternalTcp}, ExtSecureTcp: {ExternalSecureTcp}\n" +
			$"Http: {HttpEndPoint}, HttpAdvertiseAs: {AdvertiseExternalHostAs}:{AdvertiseHttpPortAs},\n" +
			$"HttpAdvertiseToClientAs: {AdvertiseHostToClientAs}:{AdvertiseHttpPortToClientAs},\n" +
			$"TcpAdvertiseToClientAs: {AdvertiseHostToClientAs}:{AdvertiseTcpPortToClientAs}");
	}
}
