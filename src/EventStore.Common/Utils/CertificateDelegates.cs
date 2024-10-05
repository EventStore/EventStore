// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace EventStore.Common.Utils;

public static class CertificateDelegates {
	public delegate (bool, string) ServerCertificateValidator(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, string[] otherNames);
	public delegate (bool, string) ClientCertificateValidator(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors);
}
