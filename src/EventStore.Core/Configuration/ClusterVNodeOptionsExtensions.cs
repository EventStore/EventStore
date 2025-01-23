// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

// ReSharper disable CheckNamespace

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Exceptions;
using EventStore.Common.Utils;
using EventStore.Plugins;
using Serilog;

namespace EventStore.Core;

public static class ClusterVNodeOptionsExtensions {
	public static ClusterVNodeOptions Reload(this ClusterVNodeOptions options) =>
		options.ConfigurationRoot == null
			? options
			: ClusterVNodeOptions.FromConfiguration(options.ConfigurationRoot);

	public static ClusterVNodeOptions WithPlugableComponent(this ClusterVNodeOptions options, IPlugableComponent plugableComponent) =>
		options with { PlugableComponents = [..options.PlugableComponents, plugableComponent] };

	/// <summary>
	/// </summary>
	/// <param name="options"></param>
	/// <returns></returns>
	/// <exception cref="InvalidConfigurationException"></exception>
	public static (X509Certificate2 certificate, X509Certificate2Collection intermediates) LoadNodeCertificate(
		this ClusterVNodeOptions options) {
		if (options.ServerCertificate != null) {
			//used by test code paths only
			return (options.ServerCertificate!, null);
		}


		if (!string.IsNullOrWhiteSpace(options.CertificateStore.CertificateStoreLocation)) {
			var location =
				CertificateUtils.GetCertificateStoreLocation(options.CertificateStore.CertificateStoreLocation);
			var name = CertificateUtils.GetCertificateStoreName(options.CertificateStore.CertificateStoreName);
			return (CertificateUtils.LoadFromStore(location, name, options.CertificateStore.CertificateSubjectName,
				options.CertificateStore.CertificateThumbprint), null);
		}

		if (!string.IsNullOrWhiteSpace(options.CertificateStore.CertificateStoreName)) {
			var name = CertificateUtils.GetCertificateStoreName(options.CertificateStore.CertificateStoreName);
			return (
				CertificateUtils.LoadFromStore(name, options.CertificateStore.CertificateSubjectName,
					options.CertificateStore.CertificateThumbprint), null);
		}

		if (options.CertificateFile.CertificateFile.IsNotEmptyString()) {
			Log.Information("Loading the node's certificate(s) from file: {path}",
				options.CertificateFile.CertificateFile);
			return CertificateUtils.LoadFromFile(options.CertificateFile.CertificateFile,
				options.CertificateFile.CertificatePrivateKeyFile, options.CertificateFile.CertificatePassword,
				options.CertificateFile.CertificatePrivateKeyPassword);
		}

		throw new InvalidConfigurationException(
			"A certificate is required unless insecure mode (--insecure) is set.");
	}

	/// <summary>
	/// Loads an <see cref="X509Certificate2Collection"/> from the options set.
	/// If either TrustedRootCertificateStoreLocation or TrustedRootCertificateStoreName is set,
	/// then the certificates will only be loaded from the certificate store.
	/// Otherwise, the certificates will be loaded from the path specified by TrustedRootCertificatesPath.
	/// </summary>
	/// <param name="options"></param>
	/// <returns></returns>
	/// <exception cref="InvalidConfigurationException"></exception>
	public static X509Certificate2Collection LoadTrustedRootCertificates(this ClusterVNodeOptions options) {
		if (options.TrustedRootCertificates != null) return options.TrustedRootCertificates;
		var trustedRootCerts = new X509Certificate2Collection();

		if (!string.IsNullOrWhiteSpace(options.CertificateStore.TrustedRootCertificateStoreLocation)) {
			var location =
				CertificateUtils.GetCertificateStoreLocation(options.CertificateStore
					.TrustedRootCertificateStoreLocation);
			var name = CertificateUtils.GetCertificateStoreName(options.CertificateStore
				.TrustedRootCertificateStoreName);
			trustedRootCerts.Add(CertificateUtils.LoadFromStore(location, name,
				options.CertificateStore.TrustedRootCertificateSubjectName,
				options.CertificateStore.TrustedRootCertificateThumbprint));
			return trustedRootCerts;
		}

		if (!string.IsNullOrWhiteSpace(options.CertificateStore.TrustedRootCertificateStoreName)) {
			var name = CertificateUtils.GetCertificateStoreName(options.CertificateStore
				.TrustedRootCertificateStoreName);
			trustedRootCerts.Add(CertificateUtils.LoadFromStore(name,
				options.CertificateStore.TrustedRootCertificateSubjectName,
				options.CertificateStore.TrustedRootCertificateThumbprint));
			return trustedRootCerts;
		}

		if (string.IsNullOrEmpty(options.Certificate.TrustedRootCertificatesPath)) {
			throw new InvalidConfigurationException(
				$"{nameof(options.Certificate.TrustedRootCertificatesPath)} must be specified unless insecure mode (--insecure) is set.");
		}

		Log.Information("Loading trusted root certificates.");
		foreach (var (fileName, cert) in CertificateUtils
			         .LoadAllCertificates(options.Certificate.TrustedRootCertificatesPath)) {
			trustedRootCerts.Add(cert);
			Log.Information("Loading trusted root certificate file: {file}", fileName);
		}

		if (trustedRootCerts.Count == 0)
			throw new InvalidConfigurationException(
				$"No trusted root certificate files were loaded from the specified path: {options.Certificate.TrustedRootCertificatesPath}");
		return trustedRootCerts;
	}
}
