// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net.NetworkInformation;

namespace EventStore.Licensing.Keygen;

// Identifies this ESDB node
public class Fingerprint {
	readonly string _nodeId;

	public Fingerprint() {
		var mac = NetworkInterface.GetAllNetworkInterfaces()
			.FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up
								   && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
			?.GetPhysicalAddress()?.ToString();
		_nodeId = mac ?? Guid.NewGuid().ToString();
	}

	public static long Ram => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

	public static int CpuCount => Environment.ProcessorCount;

	public string Get() => $"{_nodeId} {CpuCount} {Ram}";
}
