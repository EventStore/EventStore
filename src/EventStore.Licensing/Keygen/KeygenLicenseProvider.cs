// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Reactive.Linq;
using EventStore.Plugins.Licensing;
using Serilog;
using Serilog.Events;

namespace EventStore.Licensing.Keygen;

// This abstracts Keygen away from the rest of the system
// responsible for translating the keygen output into an ESDB license (or not)
// responsible for the business decisions of under what circumstances we want to grant an
//    ESDB license to the rest of the system.
public class KeygenLicenseProvider : ILicenseProvider {
	private static readonly ILogger Log = Serilog.Log.ForContext<KeygenLicenseProvider>();

	private readonly string _esdbPublicKey;
	private readonly string _esdbPrivateKey;

	public KeygenLicenseProvider(
		string esdbPublicKey,
		string esdbPrivateKey,
		IObservable<LicenseInfo> keygenLicenses) {

		_esdbPublicKey = esdbPublicKey;
		_esdbPrivateKey = esdbPrivateKey;

		// not sure that another subject is the right answer here
		var licenses = keygenLicenses
			.Select(licenseInfo => licenseInfo switch {
				// we aren't yet sure what the license status is, grant everything so we don't break a production system
				LicenseInfo.Inconclusive inconclusive => CreateLicense(inconclusive),

				// we are sure what the license status is, turn it into a ESDB license or throw
				LicenseInfo.Conclusive conclusion => CreateLicense(conclusion),

				_ => null,
			})
			.Where(x => x is not null)
			.Select(x => x!)
			.Replay(1);
		licenses.Connect();
		Licenses = licenses;
	}

	public IObservable<License> Licenses { get; private set; }

	// For some reason we were not able to detect if the users license is valid or not
	// we play it safe and grant a license that allows access to all features, to avoid
	// technical problems taking down production deployments.
	// The primary means of protection against license tampering is the license agreement
	License CreateLicense(LicenseInfo.Inconclusive licenseInfo) {
		Log.Warning("License could not be validated. Please contact EventStore support.");

		var summary = new LicenseSummary(
			LicenseId: "Fallback",
			Company: "EventStore Ltd",
			IsTrial: false,
			IsExpired: false,
			IsValid: false,
			IsFloating: false,
			StartDate: 0,
			DaysRemaining: 0,
			Notes: "License could not be validated. Please contact EventStore support.");

		return CreateLicense(summary, ["ALL"]);
	}

	License CreateLicense(LicenseInfo.Conclusive licenseInfo) {
		Log.Write(
			licenseInfo.Warning
				? LogEventLevel.Warning
				: LogEventLevel.Information,
			$"License {{Name}} {licenseInfo.Detail}. " +
			"Valid: {Valid}. Trial: {Trial}. Expiry: {Expiry:O}",
			licenseInfo.Name,
			licenseInfo.Valid, licenseInfo.Trial, licenseInfo.Expiry);

		if (licenseInfo.Expired)
			Log.Warning($"The license expired at {licenseInfo.Expiry}");

		// whether an expired license is valid or not is up to the policy in keygen
		// so we don't need any logic for it here
		if (!licenseInfo.Valid) {
			Log.Warning("License {Name} is not valid", licenseInfo.Name);
			throw new Exception($"Invalid license: {licenseInfo.Name}");
		}

		var daysRemaining = int.MaxValue;
		if (licenseInfo.Expiry.HasValue)
			daysRemaining = (int)(licenseInfo.Expiry.Value - DateTimeOffset.UtcNow).TotalDays;

		var summary = new LicenseSummary(
			LicenseId: licenseInfo.LicenseId,
			Company: licenseInfo.Name, // todo: name may not necessarily be the company name, depends what we do in the keygen dashboard
			IsTrial: licenseInfo.Trial,
			IsExpired: licenseInfo.Expired,
			IsValid: licenseInfo.Valid,
			IsFloating: true, // todo: consider if we need this
			DaysRemaining: daysRemaining,
			StartDate: 0,
			Notes: licenseInfo.Detail);

		return CreateLicense(summary, licenseInfo.Entitlements);
	}

	License CreateLicense(LicenseSummary summary, string[] entitlements) {
		var claims = entitlements.ToDictionary(
			x => x,
			x => (object)"true");

		summary?.Export(claims);

		return License.Create(
			publicKey: _esdbPublicKey,
			privateKey: _esdbPrivateKey,
			claims: claims);
	}
}
