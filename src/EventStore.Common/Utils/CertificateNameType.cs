// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Common.Utils;

public static class CertificateNameType {
	// Based on RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280)
	public const string IpAddress = "iPAddress";
	public const string DnsName = "dNSName";
}
