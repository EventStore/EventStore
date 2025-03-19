// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Reactive.Subjects;
using EventStore.Plugins.Licensing;

namespace EventStore.TcpPlugin.Tests;

class FakeLicenseService : ILicenseService {
	public FakeLicenseService(string token) {
		SelfLicense = new License(new(token));
		CurrentLicense = SelfLicense; // they wouldn't normally be the same
		Licenses = new BehaviorSubject<License>(CurrentLicense);
	}

	public License SelfLicense { get; }

	public License? CurrentLicense { get; }

	public IObservable<License> Licenses { get; }

	public void RejectLicense(Exception ex) {
	}
}
