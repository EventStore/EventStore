// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Licensing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventStore.Licensing.Tests;

public class LicenseServiceTests {
	readonly string _publicKey;
	readonly string _privateKey;

	public LicenseServiceTests() {
		using var rsa = RSA.Create(512);
		_publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
		_privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
	}

	[Fact]
	public async Task self_license_is_valid() {
		var sut = new LicenseService(_publicKey, _privateKey, new FakeLifetime(), new AdHocLicenseProvider(new Exception()));
		Assert.True(await sut.SelfLicense.ValidateAsync(_publicKey));
	}

	[Fact]
	public async Task current_license_respects_provider() {
		var licenseProvider = new AdHocLicenseProvider(License.Create(_publicKey, _privateKey));
		var sut = new LicenseService(_publicKey, _privateKey, new FakeLifetime(), licenseProvider);
		Assert.True(await sut.CurrentLicense!.ValidateAsync(_publicKey));

		licenseProvider.LicenseSubject.OnError(new Exception("an error"));
		Assert.Null(sut.CurrentLicense);
	}

	[Fact]
	public void can_reject() {
		var lifetime = new FakeLifetime();
		var licenseProvider = new AdHocLicenseProvider(License.Create(_publicKey, _privateKey));
		var sut = new LicenseService(_publicKey, _privateKey, lifetime, licenseProvider);
		var stopped = false;
		lifetime.ApplicationStopped.Register(() => stopped = true);

		sut.RejectLicense(new Exception("an error"));
		lifetime.StartApplication();

		Assert.True(stopped);
	}

	[Fact]
	public void can_reject_after_startup() {
		var lifetime = new FakeLifetime();
		var licenseProvider = new AdHocLicenseProvider(License.Create(_publicKey, _privateKey));
		var sut = new LicenseService(_publicKey, _privateKey, lifetime, licenseProvider);
		var stopped = false;
		lifetime.ApplicationStopped.Register(() => stopped = true);

		lifetime.StartApplication();
		sut.RejectLicense(new Exception("an error"));

		Assert.True(stopped);
	}

	class FakeLifetime : IHostApplicationLifetime {
		readonly CancellationTokenSource _started = new();
		readonly CancellationTokenSource _stopped = new();
		readonly CancellationTokenSource _stopping = new();

		public CancellationToken ApplicationStarted => _started.Token;

		public CancellationToken ApplicationStopped => _stopped.Token;

		public CancellationToken ApplicationStopping => _stopping.Token;

		public void StartApplication() {
			_started.Cancel();
		}

		public void StopApplication() {
			_stopping.Cancel();
			_stopped.Cancel();
		}
	}
}
