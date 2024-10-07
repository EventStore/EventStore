// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core.Certificates;

public abstract class CertificateProvider {
	public X509Certificate2 Certificate;
	public X509Certificate2Collection IntermediateCerts;
	public X509Certificate2Collection TrustedRootCerts;
	public abstract LoadCertificateResult LoadCertificates(ClusterVNodeOptions options);
	public abstract string GetReservedNodeCommonName();
}

public enum LoadCertificateResult {
	Success = 1,
	VerificationFailed,
	Skipped
}
