// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Licensing;

namespace EventStore.Licensing;


public class CurrentLicense {
	public License? License { get; set; }
	public Exception? Exception { get; set; }
}

public interface ILicenseProvider {
	// An exception can be returned as well as a non null license, to allow the provider to
	// provide a claimless license instance while still allowing the error
	// that prevented a proper license from being generated to be visible.
	IObservable<License> Licenses { get; }
}
