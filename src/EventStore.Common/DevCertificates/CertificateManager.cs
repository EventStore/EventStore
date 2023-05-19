using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using EventStore.Common.Utils;

namespace EventStore.Common.DevCertificates {

	public abstract class CertificateManager {
		internal const int CurrentCertificateVersion = 2;
		internal const string EventStoreDbHttpsOid = "1.3.6.1.4.1.43941.1.1.1";
		internal const string EventStoreHttpsOidFriendlyName = "Event Store HTTPS development certificate";

		private const string ServerAuthenticationEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.1";
		private const string ServerAuthenticationEnhancedKeyUsageOidFriendlyName = "Server Authentication";

		private const string LocalhostHttpsDnsName = "localhost";
		private const string LocalhostHttpsDistinguishedName = "CN=" + LocalhostHttpsDnsName;

		public const int RSAMinimumKeySizeInBits = 2048;

		public static CertificateManager Instance { get; } = OperatingSystem.IsWindows()
			?
#pragma warning disable CA1416 // Validate platform compatibility
			new WindowsCertificateManager()
			:
#pragma warning restore CA1416 // Validate platform compatibility
			OperatingSystem.IsMacOS()
				? new MacOSCertificateManager() as CertificateManager
				: new UnixCertificateManager();

		public static CertificateManagerEventSource Log { get; set; } = new CertificateManagerEventSource();

		// Setting to 0 means we don't append the version byte,
		// which is what all machines currently have.
		public int EventStoreHttpsCertificateVersion { get; }

		public string Subject { get; }

		protected CertificateManager() : this(LocalhostHttpsDistinguishedName, CurrentCertificateVersion) {
		}

		// For testing purposes only
		internal CertificateManager(string subject, int version) {
			Subject = subject;
			EventStoreHttpsCertificateVersion = version;
		}

		public static bool IsHttpsDevelopmentCertificate(X509Certificate2 certificate) =>
			certificate.Extensions.OfType<X509Extension>()
				.Any(e => string.Equals(EventStoreDbHttpsOid, e.Oid?.Value, StringComparison.Ordinal));

		public IList<X509Certificate2> ListCertificates(
			StoreName storeName,
			StoreLocation location,
			bool isValid,
			bool requireExportable = true) {
			Log.ListCertificatesStart(location, storeName);
			var certificates = new List<X509Certificate2>();
			try {
				using var store = new X509Store(storeName, location);
				store.Open(OpenFlags.ReadOnly);
				certificates.AddRange(store.Certificates.OfType<X509Certificate2>());
				IEnumerable<X509Certificate2> matchingCertificates = certificates;
				matchingCertificates = matchingCertificates
					.Where(c => HasOid(c, EventStoreDbHttpsOid));

				if (Log.IsEnabled()) {
					Log.DescribeFoundCertificates(ToCertificateDescription(matchingCertificates));
				}

				if (isValid) {
					// Ensure the certificate hasn't expired, has a private key and its exportable
					// (for container/unix scenarios).
					Log.CheckCertificatesValidity();
					var now = DateTimeOffset.Now;
					var validCertificates = matchingCertificates
						.Where(c => IsValidCertificate(c, now, requireExportable))
						.OrderByDescending(c => GetCertificateVersion(c))
						.ToArray();

					if (Log.IsEnabled()) {
						var invalidCertificates = matchingCertificates.Except(validCertificates);
						Log.DescribeValidCertificates(ToCertificateDescription(validCertificates));
						Log.DescribeInvalidCertificates(ToCertificateDescription(invalidCertificates));
					}

					matchingCertificates = validCertificates;
				}

				// We need to enumerate the certificates early to prevent disposing issues.
				matchingCertificates = matchingCertificates.ToList();

				var certificatesToDispose = certificates.Except(matchingCertificates);
				DisposeCertificates(certificatesToDispose);

				store.Close();

				Log.ListCertificatesEnd();
				return (IList<X509Certificate2>)matchingCertificates;
			} catch (Exception e) {
				if (Log.IsEnabled()) {
					Log.ListCertificatesError(e.ToString());
				}

				DisposeCertificates(certificates);
				certificates.Clear();
				return certificates;
			}

			bool HasOid(X509Certificate2 certificate, string oid) =>
				certificate.Extensions.OfType<X509Extension>()
					.Any(e => string.Equals(oid, e.Oid?.Value, StringComparison.Ordinal));

			static byte GetCertificateVersion(X509Certificate2 c) {
				var byteArray = c.Extensions
					.OfType<X509Extension>()
					.Single(e => string.Equals(EventStoreDbHttpsOid, e.Oid?.Value, StringComparison.Ordinal))
					.RawData;

				if ((byteArray.Length == EventStoreHttpsOidFriendlyName.Length && byteArray[0] == (byte)'A') ||
				    byteArray.Length == 0) {
					// No Version set, default to 0
					return 0b0;
				} else {
					// Version is in the only byte of the byte array.
					return byteArray[0];
				}
			}

			bool IsValidCertificate(X509Certificate2 certificate, DateTimeOffset currentDate, bool requireExportable) =>
				certificate.NotBefore <= currentDate &&
				currentDate <= certificate.NotAfter &&
				(!requireExportable || IsExportable(certificate)) &&
				GetCertificateVersion(certificate) >= EventStoreHttpsCertificateVersion;
		}

		public IList<X509Certificate2> GetHttpsCertificates() =>
			ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: true, requireExportable: true);

		public EnsureCertificateResult EnsureDevelopmentCertificate(
			DateTimeOffset notBefore,
			DateTimeOffset notAfter,
			string path = null,
			bool trust = false,
			bool includePrivateKey = false,
			string password = null,
			CertificateKeyExportFormat keyExportFormat = CertificateKeyExportFormat.Pfx,
			bool isInteractive = true) {
			var result = EnsureCertificateResult.Succeeded;

			var currentUserCertificates = ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: true,
				requireExportable: true);
			var trustedCertificates = ListCertificates(StoreName.My, StoreLocation.LocalMachine, isValid: true,
				requireExportable: true);
			var certificates = currentUserCertificates.Concat(trustedCertificates);

			var filteredCertificates = certificates.Where(c => c.Subject == Subject);

			if (Log.IsEnabled()) {
				var excludedCertificates = certificates.Except(filteredCertificates);
				Log.FilteredCertificates(ToCertificateDescription(filteredCertificates));
				Log.ExcludedCertificates(ToCertificateDescription(excludedCertificates));
			}

			certificates = filteredCertificates;

			X509Certificate2 certificate = null;
			var isNewCertificate = false;
			if (certificates.Any()) {
				certificate = certificates.First();
				var failedToFixCertificateState = false;
				if (isInteractive) {
					// Skip this step if the command is not interactive,
					// as we don't want to prompt on first run experience.
					foreach (var candidate in currentUserCertificates) {
						var status = CheckCertificateState(candidate, true);
						if (!status.Success) {
							try {
								if (Log.IsEnabled()) {
									Log.CorrectCertificateStateStart(GetDescription(candidate));
								}

								CorrectCertificateState(candidate);
								Log.CorrectCertificateStateEnd();
							} catch (Exception e) {
								if (Log.IsEnabled()) {
									Log.CorrectCertificateStateError(e.ToString());
								}

								result = EnsureCertificateResult.FailedToMakeKeyAccessible;
								// We don't return early on this type of failure to allow for tooling to
								// export or trust the certificate even in this situation, as that enables
								// exporting the certificate to perform any necessary fix with native tooling.
								failedToFixCertificateState = true;
							}
						}
					}
				}

				if (!failedToFixCertificateState) {
					if (Log.IsEnabled()) {
						Log.ValidCertificatesFound(ToCertificateDescription(certificates));
					}

					certificate = certificates.First();
					if (Log.IsEnabled()) {
						Log.SelectedCertificate(GetDescription(certificate));
					}

					result = EnsureCertificateResult.ValidCertificatePresent;
				}
			} else {
				Log.NoValidCertificatesFound();
				try {
					Log.CreateDevelopmentCertificateStart();
					isNewCertificate = true;
					certificate = CreateDevelopmentCertificate(notBefore, notAfter);
				} catch (Exception e) {
					if (Log.IsEnabled()) {
						Log.CreateDevelopmentCertificateError(e.ToString());
					}

					result = EnsureCertificateResult.ErrorCreatingTheCertificate;
					return result;
				}

				Log.CreateDevelopmentCertificateEnd();

				try {
					certificate = SaveCertificate(certificate);
				} catch (Exception e) {
					Log.SaveCertificateInStoreError(e.ToString());
					result = EnsureCertificateResult.ErrorSavingTheCertificateIntoTheCurrentUserPersonalStore;
					return result;
				}

				if (isInteractive) {
					try {
						if (Log.IsEnabled()) {
							Log.CorrectCertificateStateStart(GetDescription(certificate));
						}

						CorrectCertificateState(certificate);
						Log.CorrectCertificateStateEnd();
					} catch (Exception e) {
						if (Log.IsEnabled()) {
							Log.CorrectCertificateStateError(e.ToString());
						}

						// We don't return early on this type of failure to allow for tooling to
						// export or trust the certificate even in this situation, as that enables
						// exporting the certificate to perform any necessary fix with native tooling.
						result = EnsureCertificateResult.FailedToMakeKeyAccessible;
					}
				}
			}

			if (path != null) {
				try {
					ExportCertificate(certificate, path, includePrivateKey, password, keyExportFormat);
				} catch (Exception e) {
					if (Log.IsEnabled()) {
						Log.ExportCertificateError(e.ToString());
					}

					// We don't want to mask the original source of the error here.
					result = result != EnsureCertificateResult.Succeeded &&
					         result != EnsureCertificateResult.ValidCertificatePresent
						? result
						: EnsureCertificateResult.ErrorExportingTheCertificate;

					return result;
				}
			}

			if (trust) {
				try {
					TrustCertificate(certificate);
				} catch (UserCancelledTrustException) {
					result = EnsureCertificateResult.UserCancelledTrustStep;
					return result;
				} catch {
					result = EnsureCertificateResult.FailedToTrustTheCertificate;
					return result;
				}
			}

			DisposeCertificates(!isNewCertificate ? certificates : certificates.Append(certificate));

			return result;
		}

		internal ImportCertificateResult ImportCertificate(string certificatePath, string password) {
			if (!File.Exists(certificatePath)) {
				Log.ImportCertificateMissingFile(certificatePath);
				return ImportCertificateResult.CertificateFileMissing;
			}

			var certificates = ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false,
				requireExportable: false);
			if (certificates.Any()) {
				if (Log.IsEnabled()) {
					Log.ImportCertificateExistingCertificates(ToCertificateDescription(certificates));
				}

				return ImportCertificateResult.ExistingCertificatesPresent;
			}

			X509Certificate2 certificate;
			try {
				Log.LoadCertificateStart(certificatePath);
				certificate = new X509Certificate2(certificatePath, password,
					X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
				if (Log.IsEnabled()) {
					Log.LoadCertificateEnd(GetDescription(certificate));
				}
			} catch (Exception e) {
				if (Log.IsEnabled()) {
					Log.LoadCertificateError(e.ToString());
				}

				return ImportCertificateResult.InvalidCertificate;
			}

			if (!IsHttpsDevelopmentCertificate(certificate)) {
				if (Log.IsEnabled()) {
					Log.NoHttpsDevelopmentCertificate(GetDescription(certificate));
				}

				return ImportCertificateResult.NoDevelopmentHttpsCertificate;
			}

			try {
				SaveCertificate(certificate);
			} catch (Exception e) {
				if (Log.IsEnabled()) {
					Log.SaveCertificateInStoreError(e.ToString());
				}

				return ImportCertificateResult.ErrorSavingTheCertificateIntoTheCurrentUserPersonalStore;
			}

			return ImportCertificateResult.Succeeded;
		}

		public void CleanupHttpsCertificates() {
			// On OS X we don't have a good way to manage trusted certificates in the system keychain
			// so we do everything by invoking the native toolchain.
			// This has some limitations, like for example not being able to identify our custom OID extension. For that
			// matter, when we are cleaning up certificates on the machine, we start by removing the trusted certificates.
			// To do this, we list the certificates that we can identify on the current user personal store and we invoke
			// the native toolchain to remove them from the sytem keychain. Once we have removed the trusted certificates,
			// we remove the certificates from the local user store to finish up the cleanup.
			var certificates = ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false);
			var filteredCertificates = certificates.Where(c => c.Subject == Subject);

			if (Log.IsEnabled()) {
				var excludedCertificates = certificates.Except(filteredCertificates);
				Log.FilteredCertificates(ToCertificateDescription(filteredCertificates));
				Log.ExcludedCertificates(ToCertificateDescription(excludedCertificates));
			}

			foreach (var certificate in filteredCertificates) {
				RemoveCertificate(certificate, RemoveLocations.All);
			}
		}

		public abstract bool IsTrusted(X509Certificate2 certificate);

		protected abstract X509Certificate2 SaveCertificateCore(X509Certificate2 certificate, StoreName storeName,
			StoreLocation storeLocation);

		protected abstract void TrustCertificateCore(X509Certificate2 certificate);

		protected abstract bool IsExportable(X509Certificate2 c);

		protected abstract void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate);

		protected abstract IList<X509Certificate2> GetCertificatesToRemove(StoreName storeName,
			StoreLocation storeLocation);

		internal static void ExportCertificate(X509Certificate2 certificate, string path, bool includePrivateKey,
			string password, CertificateKeyExportFormat format) {
			if (Log.IsEnabled()) {
				Log.ExportCertificateStart(GetDescription(certificate), path, includePrivateKey);
			}

			if (includePrivateKey && password == null) {
				Log.NoPasswordForCertificate();
			}

			var targetDirectoryPath = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(targetDirectoryPath)) {
				Log.CreateExportCertificateDirectory(targetDirectoryPath);
				Directory.CreateDirectory(targetDirectoryPath);
			}

			byte[] bytes;
			byte[] keyBytes;
			byte[] pemEnvelope = null;
			RSA key = null;

			try {
				if (includePrivateKey) {
					switch (format) {
						case CertificateKeyExportFormat.Pfx:
							bytes = certificate.ExportToPkcs12(password);
							break;
						case CertificateKeyExportFormat.Pem:
							key = certificate.GetRSAPrivateKey()!;

							char[] pem;
							if (password != null) {
								// TODO: cleanup cast: https://github.com/dotnet/aspnetcore/issues/41455
								keyBytes = key.ExportEncryptedPkcs8PrivateKey((ReadOnlySpan<char>)password,
									new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256,
										100000));
								pem = PemEncoding.Write("ENCRYPTED PRIVATE KEY", keyBytes);
								pemEnvelope = Encoding.ASCII.GetBytes(pem);
							} else {
								// Export the key first to an encrypted PEM to avoid issues with System.Security.Cryptography.Cng indicating that the operation is not supported.
								// This is likely by design to avoid exporting the key by mistake.
								// To bypass it, we export the certificate to pem temporarily and then we import it and export it as unprotected PEM.
								// TODO: cleanup cast: https://github.com/dotnet/aspnetcore/issues/41455
								keyBytes = key.ExportEncryptedPkcs8PrivateKey((ReadOnlySpan<char>)"",
									new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1));
								pem = PemEncoding.Write("ENCRYPTED PRIVATE KEY", keyBytes);
								key.Dispose();
								key = RSA.Create();
								// TODO: cleanup cast: https://github.com/dotnet/aspnetcore/issues/41455
								key.ImportFromEncryptedPem(pem, (ReadOnlySpan<char>)"");
								Array.Clear(keyBytes, 0, keyBytes.Length);
								Array.Clear(pem, 0, pem.Length);
								keyBytes = key.ExportPkcs8PrivateKey();
								pem = PemEncoding.Write("PRIVATE KEY", keyBytes);
								pemEnvelope = Encoding.ASCII.GetBytes(pem);
							}

							Array.Clear(keyBytes, 0, keyBytes.Length);
							Array.Clear(pem, 0, pem.Length);

							bytes = Encoding.ASCII.GetBytes(PemEncoding.Write("CERTIFICATE",
								certificate.Export(X509ContentType.Cert)));
							break;
						default:
							throw new InvalidOperationException("Unknown format.");
					}
				} else {
					if (format == CertificateKeyExportFormat.Pem) {
						bytes = Encoding.ASCII.GetBytes(PemEncoding.Write("CERTIFICATE",
							certificate.Export(X509ContentType.Cert)));
					} else {
						bytes = certificate.Export(X509ContentType.Cert);
					}
				}
			} catch (Exception e) when (Log.IsEnabled()) {
				Log.ExportCertificateError(e.ToString());
				throw;
			} finally {
				key?.Dispose();
			}

			try {
				Log.WriteCertificateToDisk(path);
				File.WriteAllBytes(path, bytes);
			} catch (Exception ex) when (Log.IsEnabled()) {
				Log.WriteCertificateToDiskError(ex.ToString());
				throw;
			} finally {
				Array.Clear(bytes, 0, bytes.Length);
			}

			if (includePrivateKey && format == CertificateKeyExportFormat.Pem) {
				Debug.Assert(pemEnvelope != null);

				try {
					var keyPath = Path.ChangeExtension(path, ".key");
					Log.WritePemKeyToDisk(keyPath);
					File.WriteAllBytes(keyPath, pemEnvelope);
				} catch (Exception ex) when (Log.IsEnabled()) {
					Log.WritePemKeyToDiskError(ex.ToString());
					throw;
				} finally {
					Array.Clear(pemEnvelope, 0, pemEnvelope.Length);
				}
			}
		}

		internal X509Certificate2 CreateDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter) {
			var subject = new X500DistinguishedName(Subject);
			var extensions = new List<X509Extension>();
			var sanBuilder = new SubjectAlternativeNameBuilder();
			sanBuilder.AddDnsName(LocalhostHttpsDnsName);

			var keyUsage =
				new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature,
					critical: true);
			var enhancedKeyUsage = new X509EnhancedKeyUsageExtension(
				new OidCollection() {
					new Oid(
						ServerAuthenticationEnhancedKeyUsageOid,
						ServerAuthenticationEnhancedKeyUsageOidFriendlyName)
				},
				critical: true);

			var basicConstraints = new X509BasicConstraintsExtension(
				certificateAuthority: false,
				hasPathLengthConstraint: false,
				pathLengthConstraint: 0,
				critical: true);

			byte[] bytePayload;

			if (EventStoreHttpsCertificateVersion != 0) {
				bytePayload = new byte[1];
				bytePayload[0] = (byte)EventStoreHttpsCertificateVersion;
			} else {
				bytePayload = Encoding.ASCII.GetBytes(EventStoreHttpsOidFriendlyName);
			}

			var aspNetHttpsExtension = new X509Extension(
				new AsnEncodedData(
					new Oid(EventStoreDbHttpsOid, EventStoreHttpsOidFriendlyName),
					bytePayload),
				critical: false);

			extensions.Add(basicConstraints);
			extensions.Add(keyUsage);
			extensions.Add(enhancedKeyUsage);
			extensions.Add(sanBuilder.Build(critical: true));
			extensions.Add(aspNetHttpsExtension);

			var certificate = CreateSelfSignedCertificate(subject, extensions, notBefore, notAfter);
			return certificate;
		}

		internal X509Certificate2 SaveCertificate(X509Certificate2 certificate) {
			var name = StoreName.My;
			var location = StoreLocation.CurrentUser;

			if (Log.IsEnabled()) {
				Log.SaveCertificateInStoreStart(GetDescription(certificate), name, location);
			}

			certificate = SaveCertificateCore(certificate, name, location);

			Log.SaveCertificateInStoreEnd();
			return certificate;
		}

		public void TrustCertificate(X509Certificate2 certificate) {
			try {
				if (Log.IsEnabled()) {
					Log.TrustCertificateStart(GetDescription(certificate));
				}

				TrustCertificateCore(certificate);
				Log.TrustCertificateEnd();
			} catch (Exception ex) when (Log.IsEnabled()) {
				Log.TrustCertificateError(ex.ToString());
				throw;
			}
		}

		// Internal, for testing purposes only.
		internal void RemoveAllCertificates(StoreName storeName, StoreLocation storeLocation) {
			var certificates = GetCertificatesToRemove(storeName, storeLocation);
			var certificatesWithName = certificates.Where(c => c.Subject == Subject);

			var removeLocation = storeName == StoreName.My ? RemoveLocations.Local : RemoveLocations.Trusted;

			foreach (var certificate in certificates) {
				RemoveCertificate(certificate, removeLocation);
			}

			DisposeCertificates(certificates);
		}

		internal void RemoveCertificate(X509Certificate2 certificate, RemoveLocations locations) {
			switch (locations) {
				case RemoveLocations.Undefined:
					throw new InvalidOperationException(
						$"'{nameof(RemoveLocations.Undefined)}' is not a valid location.");
				case RemoveLocations.Local:
					RemoveCertificateFromUserStore(certificate);
					break;
				case RemoveLocations.Trusted:
					RemoveCertificateFromTrustedRoots(certificate);
					break;
				case RemoveLocations.All:
					RemoveCertificateFromTrustedRoots(certificate);
					RemoveCertificateFromUserStore(certificate);
					break;
				default:
					throw new InvalidOperationException("Invalid location.");
			}
		}

		internal abstract CheckCertificateStateResult CheckCertificateState(X509Certificate2 candidate,
			bool interactive);

		internal abstract void CorrectCertificateState(X509Certificate2 candidate);

		internal static X509Certificate2 CreateSelfSignedCertificate(
			X500DistinguishedName subject,
			IEnumerable<X509Extension> extensions,
			DateTimeOffset notBefore,
			DateTimeOffset notAfter) {
			using var key = CreateKeyMaterial(RSAMinimumKeySizeInBits);

			var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			foreach (var extension in extensions) {
				request.CertificateExtensions.Add(extension);
			}

			var result = request.CreateSelfSigned(notBefore, notAfter);
			return result;

			static RSA CreateKeyMaterial(int minimumKeySize) {
				var rsa = RSA.Create(minimumKeySize);
				if (rsa.KeySize < minimumKeySize) {
					throw new InvalidOperationException($"Failed to create a key with a size of {minimumKeySize} bits");
				}

				return rsa;
			}
		}

		internal static void DisposeCertificates(IEnumerable<X509Certificate2> disposables) {
			foreach (var disposable in disposables) {
				try {
					disposable.Dispose();
				} catch {
				}
			}
		}

		private static void RemoveCertificateFromUserStore(X509Certificate2 certificate) {
			try {
				if (Log.IsEnabled()) {
					Log.RemoveCertificateFromUserStoreStart(GetDescription(certificate));
				}

				using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
				store.Open(OpenFlags.ReadWrite);
				var matching = store.Certificates
					.OfType<X509Certificate2>()
					.Single(c => c.SerialNumber == certificate.SerialNumber);

				store.Remove(matching);
				store.Close();
				Log.RemoveCertificateFromUserStoreEnd();
			} catch (Exception ex) when (Log.IsEnabled()) {
				Log.RemoveCertificateFromUserStoreError(ex.ToString());
				throw;
			}
		}

		internal static string ToCertificateDescription(IEnumerable<X509Certificate2> certificates) {
			var list = certificates.ToList();
			var certificatesDescription = list.Count switch {
				0 => "no certificates",
				1 => "1 certificate",
				_ => $"{list.Count} certificates",
			};
			var description = list.OrderBy(c => c.Thumbprint).Select((c, i) => $"    {i + 1}) " + GetDescription(c))
				.Prepend(certificatesDescription);
			return string.Join(Environment.NewLine, description);
		}

		internal static string GetDescription(X509Certificate2 c) =>
			$"{c.Thumbprint} - {c.Subject} - Valid from {c.NotBefore:u} to {c.NotAfter:u} - IsEventStoreDevelopmentCertificate: {IsHttpsDevelopmentCertificate(c).ToString().ToLowerInvariant()} - IsExportable: {Instance.IsExportable(c).ToString().ToLowerInvariant()}";

		[EventSource(Name = "eventstore-dev-certs")]
		public sealed class CertificateManagerEventSource : EventSource {
			[Event(1, Level = EventLevel.Verbose, Message = "Listing certificates from {0}\\{1}")]
			[UnconditionalSuppressMessage("Trimming", "IL2026",
				Justification = "Parameters passed to WriteEvent are all primitive values.")]
			public void ListCertificatesStart(StoreLocation location, StoreName storeName) =>
				WriteEvent(1, location, storeName);

			[Event(2, Level = EventLevel.Verbose, Message = "Found certificates: {0}")]
			public void DescribeFoundCertificates(string matchingCertificates) => WriteEvent(2, matchingCertificates);

			[Event(3, Level = EventLevel.Verbose, Message = "Checking certificates validity")]
			public void CheckCertificatesValidity() => WriteEvent(3);

			[Event(4, Level = EventLevel.Verbose, Message = "Valid certificates: {0}")]
			public void DescribeValidCertificates(string validCertificates) => WriteEvent(4, validCertificates);

			[Event(5, Level = EventLevel.Verbose, Message = "Invalid certificates: {0}")]
			public void DescribeInvalidCertificates(string invalidCertificates) => WriteEvent(5, invalidCertificates);

			[Event(6, Level = EventLevel.Verbose, Message = "Finished listing certificates.")]
			public void ListCertificatesEnd() => WriteEvent(6);

			[Event(7, Level = EventLevel.Error, Message = "An error occurred while listing the certificates: {0}")]
			public void ListCertificatesError(string e) => WriteEvent(7, e);

			[Event(8, Level = EventLevel.Verbose, Message = "Filtered certificates: {0}")]
			public void FilteredCertificates(string filteredCertificates) => WriteEvent(8, filteredCertificates);

			[Event(9, Level = EventLevel.Verbose, Message = "Excluded certificates: {0}")]
			public void ExcludedCertificates(string excludedCertificates) => WriteEvent(9, excludedCertificates);

			[Event(14, Level = EventLevel.Verbose, Message = "Valid certificates: {0}")]
			public void ValidCertificatesFound(string certificates) => WriteEvent(14, certificates);

			[Event(15, Level = EventLevel.Verbose, Message = "Selected certificate: {0}")]
			public void SelectedCertificate(string certificate) => WriteEvent(15, certificate);

			[Event(16, Level = EventLevel.Verbose, Message = "No valid certificates found.")]
			public void NoValidCertificatesFound() => WriteEvent(16);

			[Event(17, Level = EventLevel.Verbose, Message = "Generating HTTPS development certificate.")]
			public void CreateDevelopmentCertificateStart() => WriteEvent(17);

			[Event(18, Level = EventLevel.Verbose, Message = "Finished generating HTTPS development certificate.")]
			public void CreateDevelopmentCertificateEnd() => WriteEvent(18);

			[Event(19, Level = EventLevel.Error, Message = "An error has occurred generating the certificate: {0}.")]
			public void CreateDevelopmentCertificateError(string e) => WriteEvent(19, e);

			[Event(20, Level = EventLevel.Verbose, Message = "Saving certificate '{0}' to store {2}\\{1}.")]
			[UnconditionalSuppressMessage("Trimming", "IL2026",
				Justification = "Parameters passed to WriteEvent are all primitive values.")]
			public void SaveCertificateInStoreStart(string certificate, StoreName name, StoreLocation location) =>
				WriteEvent(20, certificate, name, location);

			[Event(21, Level = EventLevel.Verbose, Message = "Finished saving certificate to the store.")]
			public void SaveCertificateInStoreEnd() => WriteEvent(21);

			[Event(22, Level = EventLevel.Error, Message = "An error has occurred saving the certificate: {0}.")]
			public void SaveCertificateInStoreError(string e) => WriteEvent(22, e);

			[Event(23, Level = EventLevel.Verbose, Message = "Saving certificate '{0}' to {1} {2} private key.")]
			public void ExportCertificateStart(string certificate, string path, bool includePrivateKey) =>
				WriteEvent(23, certificate, path, includePrivateKey ? "with" : "without");

			[Event(24, Level = EventLevel.Verbose, Message = "Exporting certificate with private key but no password.")]
			public void NoPasswordForCertificate() => WriteEvent(24);

			[Event(25, Level = EventLevel.Verbose, Message = "Creating directory {0}.")]
			public void CreateExportCertificateDirectory(string path) => WriteEvent(25, path);

			[Event(26, Level = EventLevel.Error,
				Message = "An error has occurred while exporting the certificate: {0}.")]
			public void ExportCertificateError(string error) => WriteEvent(26, error);

			[Event(27, Level = EventLevel.Verbose, Message = "Writing the certificate to: {0}.")]
			public void WriteCertificateToDisk(string path) => WriteEvent(27, path);

			[Event(28, Level = EventLevel.Error,
				Message = "An error has occurred while writing the certificate to disk: {0}.")]
			public void WriteCertificateToDiskError(string error) => WriteEvent(28, error);

			[Event(29, Level = EventLevel.Verbose, Message = "Trusting the certificate to: {0}.")]
			public void TrustCertificateStart(string certificate) => WriteEvent(29, certificate);

			[Event(30, Level = EventLevel.Verbose, Message = "Finished trusting the certificate.")]
			public void TrustCertificateEnd() => WriteEvent(30);

			[Event(31, Level = EventLevel.Error,
				Message = "An error has occurred while trusting the certificate: {0}.")]
			public void TrustCertificateError(string error) => WriteEvent(31, error);

			[Event(32, Level = EventLevel.Verbose, Message = "Running the trust command {0}.")]
			public void MacOSTrustCommandStart(string command) => WriteEvent(32, command);

			[Event(33, Level = EventLevel.Verbose, Message = "Finished running the trust command.")]
			public void MacOSTrustCommandEnd() => WriteEvent(33);

			[Event(34, Level = EventLevel.Warning,
				Message = "An error has occurred while running the trust command: {0}.")]
			public void MacOSTrustCommandError(int exitCode) => WriteEvent(34, exitCode);

			[Event(35, Level = EventLevel.Verbose, Message = "Running the remove trust command for {0}.")]
			public void MacOSRemoveCertificateTrustRuleStart(string certificate) => WriteEvent(35, certificate);

			[Event(36, Level = EventLevel.Verbose, Message = "Finished running the remove trust command.")]
			public void MacOSRemoveCertificateTrustRuleEnd() => WriteEvent(36);

			[Event(37, Level = EventLevel.Warning,
				Message = "An error has occurred while running the remove trust command: {0}.")]
			public void MacOSRemoveCertificateTrustRuleError(int exitCode) => WriteEvent(37, exitCode);

			[Event(38, Level = EventLevel.Verbose, Message = "The certificate is not trusted: {0}.")]
			public void MacOSCertificateUntrusted(string certificate) => WriteEvent(38, certificate);

			[Event(39, Level = EventLevel.Verbose, Message = "Removing the certificate from the keychain {0} {1}.")]
			public void MacOSRemoveCertificateFromKeyChainStart(string keyChain, string certificate) =>
				WriteEvent(39, keyChain, certificate);

			[Event(40, Level = EventLevel.Verbose, Message = "Finished removing the certificate from the keychain.")]
			public void MacOSRemoveCertificateFromKeyChainEnd() => WriteEvent(40);

			[Event(41, Level = EventLevel.Warning,
				Message = "An error has occurred while running the remove trust command: {0}.")]
			public void MacOSRemoveCertificateFromKeyChainError(int exitCode) => WriteEvent(41, exitCode);

			[Event(42, Level = EventLevel.Verbose, Message = "Removing the certificate from the user store {0}.")]
			public void RemoveCertificateFromUserStoreStart(string certificate) => WriteEvent(42, certificate);

			[Event(43, Level = EventLevel.Verbose, Message = "Finished removing the certificate from the user store.")]
			public void RemoveCertificateFromUserStoreEnd() => WriteEvent(43);

			[Event(44, Level = EventLevel.Error,
				Message = "An error has occurred while removing the certificate from the user store: {0}.")]
			public void RemoveCertificateFromUserStoreError(string error) => WriteEvent(44, error);

			[Event(45, Level = EventLevel.Verbose,
				Message = "Adding certificate to the trusted root certification authority store.")]
			public void WindowsAddCertificateToRootStore() => WriteEvent(45);

			[Event(46, Level = EventLevel.Verbose, Message = "The certificate is already trusted.")]
			public void WindowsCertificateAlreadyTrusted() => WriteEvent(46);

			[Event(47, Level = EventLevel.Verbose, Message = "Trusting the certificate was cancelled by the user.")]
			public void WindowsCertificateTrustCanceled() => WriteEvent(47);

			[Event(48, Level = EventLevel.Verbose,
				Message = "Removing the certificate from the trusted root certification authority store.")]
			public void WindowsRemoveCertificateFromRootStoreStart() => WriteEvent(48);

			[Event(49, Level = EventLevel.Verbose,
				Message = "Finished removing the certificate from the trusted root certification authority store.")]
			public void WindowsRemoveCertificateFromRootStoreEnd() => WriteEvent(49);

			[Event(50, Level = EventLevel.Verbose, Message = "The certificate was not trusted.")]
			public void WindowsRemoveCertificateFromRootStoreNotFound() => WriteEvent(50);

			[Event(51, Level = EventLevel.Verbose, Message = "Correcting the the certificate state for '{0}'.")]
			public void CorrectCertificateStateStart(string certificate) => WriteEvent(51, certificate);

			[Event(52, Level = EventLevel.Verbose, Message = "Finished correcting the certificate state.")]
			public void CorrectCertificateStateEnd() => WriteEvent(52);

			[Event(53, Level = EventLevel.Error,
				Message = "An error has occurred while correcting the certificate state: {0}.")]
			public void CorrectCertificateStateError(string error) => WriteEvent(53, error);

			[Event(54, Level = EventLevel.Verbose, Message = "Importing the certificate {1} to the keychain '{0}'.")]
			internal void MacOSAddCertificateToKeyChainStart(string keychain, string certificate) =>
				WriteEvent(54, keychain, certificate);

			[Event(55, Level = EventLevel.Verbose, Message = "Finished importing the certificate to the keychain.")]
			internal void MacOSAddCertificateToKeyChainEnd() => WriteEvent(55);

			[Event(56, Level = EventLevel.Error,
				Message = "An error has occurred while importing the certificate to the keychain: {0}.")]
			internal void MacOSAddCertificateToKeyChainError(int exitCode) => WriteEvent(56, exitCode);

			[Event(57, Level = EventLevel.Verbose, Message = "Writing the certificate to: {0}.")]
			public void WritePemKeyToDisk(string path) => WriteEvent(57, path);

			[Event(58, Level = EventLevel.Error,
				Message = "An error has occurred while writing the certificate to disk: {0}.")]
			public void WritePemKeyToDiskError(string error) => WriteEvent(58, error);

			[Event(59, Level = EventLevel.Error, Message = "The file '{0}' does not exist.")]
			internal void ImportCertificateMissingFile(string certificatePath) => WriteEvent(59, certificatePath);

			[Event(60, Level = EventLevel.Error, Message = "One or more HTTPS certificates exist '{0}'.")]
			internal void ImportCertificateExistingCertificates(string certificateDescription) =>
				WriteEvent(60, certificateDescription);

			[Event(61, Level = EventLevel.Verbose, Message = "Loading certificate from path '{0}'.")]
			internal void LoadCertificateStart(string certificatePath) => WriteEvent(61, certificatePath);

			[Event(62, Level = EventLevel.Verbose, Message = "The certificate '{0}' has been loaded successfully.")]
			internal void LoadCertificateEnd(string description) => WriteEvent(62, description);

			[Event(63, Level = EventLevel.Error,
				Message = "An error has occurred while loading the certificate from disk: {0}.")]
			internal void LoadCertificateError(string error) => WriteEvent(63, error);

			[Event(64, Level = EventLevel.Error,
				Message = "The provided certificate '{0}' is not a valid Event Store HTTPS development certificate.")]
			internal void NoHttpsDevelopmentCertificate(string description) => WriteEvent(64, description);
		}

		internal sealed class UserCancelledTrustException : Exception {
		}

		internal readonly struct CheckCertificateStateResult {
			public bool Success { get; }
			public string FailureMessage { get; }

			public CheckCertificateStateResult(bool success, string failureMessage) {
				Success = success;
				FailureMessage = failureMessage;
			}
		}

		internal enum RemoveLocations {
			Undefined,
			Local,
			Trusted,
			All
		}
	}
}
