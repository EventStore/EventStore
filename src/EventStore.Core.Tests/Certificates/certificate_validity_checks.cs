// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Core.Tests.Certificates;

public class validity_checks_for_chain_of_length_1 : with_certificate_chain_of_length_1 {
	[Test]
	public void leaf_is_valid_node_certificate() => Assert.True(CertificateUtils.IsValidNodeCertificate(_leaf, out _));

	[Test]
	public void leaf_is_not_valid_intermediate() => Assert.False(CertificateUtils.IsValidIntermediateCertificate(_leaf, out _));

	[Test]
	public void leaf_is_valid_root() => Assert.True(CertificateUtils.IsValidRootCertificate(_leaf, out _));
}

public class validity_checks_for_chain_of_length_2 : with_certificate_chain_of_length_2 {
	[Test]
	public void leaf_is_valid_node_certificate() => Assert.True(CertificateUtils.IsValidNodeCertificate(_leaf, out _));

	[Test]
	public void leaf_is_valid_intermediate() => Assert.True(CertificateUtils.IsValidIntermediateCertificate(_leaf, out _));

	[Test]
	public void leaf_is_not_valid_root() => Assert.False(CertificateUtils.IsValidRootCertificate(_leaf, out _));

	[Test]
	public void root_is_not_valid_node_certificate() => Assert.False(CertificateUtils.IsValidNodeCertificate(_root, out _));

	[Test]
	public void root_is_not_valid_intermediate() => Assert.False(CertificateUtils.IsValidIntermediateCertificate(_root, out _));

	[Test]
	public void root_is_valid_root() => Assert.True(CertificateUtils.IsValidRootCertificate(_root, out _));
}

public class validity_checks_for_chain_of_length_3 : with_certificate_chain_of_length_3 {
	[Test]
	public void leaf_is_valid_node_certificate() => Assert.True(CertificateUtils.IsValidNodeCertificate(_leaf, out _));

	[Test]
	public void leaf_is_valid_intermediate() => Assert.True(CertificateUtils.IsValidIntermediateCertificate(_leaf, out _));

	[Test]
	public void leaf_is_not_valid_root() => Assert.False(CertificateUtils.IsValidRootCertificate(_leaf, out _));

	[Test]
	public void intermediate_is_not_valid_node_certificate() => Assert.False(CertificateUtils.IsValidNodeCertificate(_intermediate, out _));

	[Test]
	public void intermediate_is_valid_intermediate() => Assert.True(CertificateUtils.IsValidIntermediateCertificate(_intermediate, out _));

	[Test]
	public void intermediate_is_not_valid_root() => Assert.False(CertificateUtils.IsValidRootCertificate(_intermediate, out _));

	[Test]
	public void root_is_not_valid_node_certificate() => Assert.False(CertificateUtils.IsValidNodeCertificate(_root, out _));

	[Test]
	public void root_is_not_valid_intermediate() => Assert.False(CertificateUtils.IsValidIntermediateCertificate(_root, out _));

	[Test]
	public void root_is_valid_root() => Assert.True(CertificateUtils.IsValidRootCertificate(_root, out _));
}
