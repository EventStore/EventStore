namespace EventStore.Common.DevCertificates {
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
}
