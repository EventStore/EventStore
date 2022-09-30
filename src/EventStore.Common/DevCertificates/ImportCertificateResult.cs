namespace EventStore.Common.DevCertificates {
	public enum ImportCertificateResult {
		Succeeded = 1,
		CertificateFileMissing,
		InvalidCertificate,
		NoDevelopmentHttpsCertificate,
		ExistingCertificatesPresent,
		ErrorSavingTheCertificateIntoTheCurrentUserPersonalStore,
	}
}
