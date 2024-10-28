// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using EventStore.Licensing.Keygen;
using Xunit;

namespace EventStore.Licensing.Tests.Keygen;

public class KeygenLicenseProviderTests {
	[Fact]
	public async Task conclusive_valid() {
		var licenses = new Subject<LicenseInfo>();
		var sut = new KeygenLicenseProvider(
			licenses);

		licenses.OnNext(new LicenseInfo.Conclusive(
			LicenseId: "license id",
			Name: "acme",
			Valid: true,
			Trial: false,
			Warning: false,
			Detail: "is lookin good",
			Expiry: DateTimeOffset.UtcNow + TimeSpan.FromHours(24 + 24 + 1),
			Entitlements: ["FUTURE_TECH_3"]));

		licenses.OnCompleted();
		var esdbLicense = (await sut.Licenses.ToList()).Single();

		Assert.True(await esdbLicense.ValidateAsync());
		Assert.True(esdbLicense.HasEntitlement("FUTURE_TECH_3"));

		var endpointInfo = LicenseSummary.SelectForEndpoint(esdbLicense);
		Assert.Equal(9, endpointInfo.Count());
		Assert.Equal("license id", endpointInfo["licenseId"]);
		Assert.Equal("acme", endpointInfo["company"]);
		Assert.Equal("false", endpointInfo["isTrial"]);
		Assert.Equal("false", endpointInfo["isExpired"]);
		Assert.Equal("true", endpointInfo["isValid"]);
		Assert.Equal("true", endpointInfo["isFloating"]);
		Assert.Equal("2", endpointInfo["daysRemaining"]);
		Assert.Equal("0", endpointInfo["startDate"]);
		Assert.Equal("is lookin good", endpointInfo["notes"]);

		var telemetryInfo = LicenseSummary.SelectForTelemetry(esdbLicense);
		Assert.Equal(4, telemetryInfo.Count());
		Assert.Equal("license id", telemetryInfo["licenseId"]);
		Assert.Equal(false, telemetryInfo["isTrial"]);
		Assert.Equal(false, telemetryInfo["isExpired"]);
		Assert.Equal(true, telemetryInfo["isValid"]);
	}

	[Fact]
	public async Task conclusive_invalid() {
		var licenses = new Subject<LicenseInfo>();
		var sut = new KeygenLicenseProvider(
			licenses);

		licenses.OnNext(new LicenseInfo.Conclusive(
			LicenseId: "license id",
			Name: "acme",
			Valid: false,
			Trial: false,
			Warning: false,
			Detail: "is not valid",
			Expiry: DateTimeOffset.UtcNow + TimeSpan.FromHours(24 + 24 + 1),
			Entitlements: ["FUTURE_TECH_3"]));

		licenses.OnCompleted();

		var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await sut.Licenses.ToList());
		Assert.Equal("Invalid license: acme", ex.Message);
	}

	[Fact]
	public async Task inconclusive() {
		var licenses = new Subject<LicenseInfo>();
		var sut = new KeygenLicenseProvider(
			licenses);

		licenses.OnNext(new LicenseInfo.Inconclusive());
		licenses.OnCompleted();
		var esdbLicense = (await sut.Licenses.ToList()).Single();

		Assert.True(await esdbLicense.ValidateAsync());
		Assert.True(esdbLicense.HasEntitlement("ALL"));

		var endpointInfo = LicenseSummary.SelectForEndpoint(esdbLicense);
		Assert.Equal(9, endpointInfo.Count());
		Assert.Equal("Fallback", endpointInfo["licenseId"]);
		Assert.Equal("EventStore Ltd", endpointInfo["company"]);
		Assert.Equal("false", endpointInfo["isTrial"]);
		Assert.Equal("false", endpointInfo["isExpired"]);
		Assert.Equal("false", endpointInfo["isValid"]);
		Assert.Equal("false", endpointInfo["isFloating"]);
		Assert.Equal("0", endpointInfo["daysRemaining"]);
		Assert.Equal("0", endpointInfo["startDate"]);
		Assert.Equal("License could not be validated. Please contact EventStore support.", endpointInfo["notes"]);

		var telemetryInfo = LicenseSummary.SelectForTelemetry(esdbLicense);
		Assert.Equal(4, telemetryInfo.Count());
		Assert.Equal("Fallback", telemetryInfo["licenseId"]);
		Assert.Equal(false, telemetryInfo["isTrial"]);
		Assert.Equal(false, telemetryInfo["isExpired"]);
		Assert.Equal(false, telemetryInfo["isValid"]);
	}
}
