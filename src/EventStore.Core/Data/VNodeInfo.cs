// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data;

public class VNodeInfo {
	public readonly Guid InstanceId;
	public readonly int DebugIndex;
	public readonly IPEndPoint InternalTcp;
	public readonly IPEndPoint InternalSecureTcp;
	public readonly IPEndPoint ExternalTcp;
	public readonly IPEndPoint ExternalSecureTcp;
	public readonly EndPoint HttpEndPoint;
	public readonly bool IsReadOnlyReplica;

	public VNodeInfo(Guid instanceId, int debugIndex,
		IPEndPoint internalTcp, IPEndPoint internalSecureTcp,
		IPEndPoint externalTcp, IPEndPoint externalSecureTcp,
		EndPoint httpEndPoint,
		bool isReadOnlyReplica) {
		Ensure.NotEmptyGuid(instanceId, "instanceId");
		Ensure.Equal(false, internalTcp == null && internalSecureTcp == null, "Both internal TCP endpoints are null");
		Ensure.NotNull(httpEndPoint, nameof(httpEndPoint));

		DebugIndex = debugIndex;
		InstanceId = instanceId;
		InternalTcp = internalTcp;
		InternalSecureTcp = internalSecureTcp;
		ExternalTcp = externalTcp;
		ExternalSecureTcp = externalSecureTcp;
		HttpEndPoint = httpEndPoint;
		IsReadOnlyReplica = isReadOnlyReplica;
	}

	public bool Is(EndPoint endPoint) {
		return endPoint != null
		       && HttpEndPoint.Equals(endPoint)
		           || (InternalTcp != null && InternalTcp.Equals(endPoint))
		           || (InternalSecureTcp != null && InternalSecureTcp.Equals(endPoint))
		           || (ExternalTcp != null && ExternalTcp.Equals(endPoint))
		           || (ExternalSecureTcp != null && ExternalSecureTcp.Equals(endPoint));
	}

	public override string ToString() {
		return $"InstanceId: {InstanceId:B}, InternalTcp: {InternalTcp}, InternalSecureTcp: {InternalSecureTcp}, " +
		       $"ExternalTcp: {ExternalTcp}, ExternalSecureTcp: {ExternalSecureTcp}, HttpEndPoint: {HttpEndPoint}," + $"IsReadOnlyReplica: {IsReadOnlyReplica}";
	}
}
