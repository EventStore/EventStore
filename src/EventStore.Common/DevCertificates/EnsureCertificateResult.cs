// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Common.DevCertificates;

public enum EnsureCertificateResult {
	Succeeded = 1,
	ValidCertificatePresent,
	ErrorCreatingTheCertificate,
	ErrorSavingTheCertificateIntoTheCurrentUserPersonalStore,
	ErrorExportingTheCertificate,
	FailedToTrustTheCertificate,
	UserCancelledTrustStep,
	FailedToMakeKeyAccessible,
}
