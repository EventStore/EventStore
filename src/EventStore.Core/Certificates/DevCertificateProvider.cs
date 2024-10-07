// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;

namespace EventStore.Core.Certificates;

public class DevCertificateProvider : CertificateProvider {
	public DevCertificateProvider(X509Certificate2 certificate) {
		Certificate = certificate;
		TrustedRootCerts = new X509Certificate2Collection(certificate);
	}
	public override LoadCertificateResult LoadCertificates(ClusterVNodeOptions options) {
		return LoadCertificateResult.Skipped;
	}

	public override string GetReservedNodeCommonName() {
		return Certificate.GetCommonName();
	}
}
