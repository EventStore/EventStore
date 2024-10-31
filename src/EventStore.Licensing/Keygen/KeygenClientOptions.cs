// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Licensing.Keygen;

public record KeygenClientOptions {
	public int NodePort { get; set; }

	public bool Archiver { get; set; }

	public bool ReadOnlyReplica { get; set; }

	public LicensingOptions Licensing { get; set; } = new();

	public record LicensingOptions {
		public string LicenseKey { get; set; } = "";

		public string? BaseUrl { get; set; }

		public bool IncludePortInFingerprint { get; set; }
	}
}
