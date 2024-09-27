// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core.Tests.Certificates {
	public class with_certificate_chain_of_length_1 : with_certificates {
		protected readonly X509Certificate2 _leaf;

		public with_certificate_chain_of_length_1() {
			_leaf = CreateCertificate(issuer: false);
		}
	}
}
