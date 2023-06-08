using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace EventStore.Common.Utils {
	public static class CertificateExtensions {
		public static IEnumerable<(string name, string type)> GetSubjectAlternativeNames(this X509Certificate certificate) {
			// Implemented based on RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280)
			// - Reads IP addresses and DNS names from the Subject Alternative Names extension
			// - Does not support other name types yet

			if (certificate is not X509Certificate2 certificate2) {
				using var c2 = new X509Certificate2(certificate);
				certificate2 = c2;
			}

			X509ExtensionCollection extensions;
			try {
				extensions = certificate2.Extensions;
			} catch (CryptographicException) {
				return null;
			}

			var sans = new List<(string, string)>();
			foreach (var extension in extensions) {
				if (extension.Oid?.Value != "2.5.29.17") continue; // Oid for Subject Alternative Names extension
				var asnReader = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
				while (asnReader.HasData) {
					Asn1Tag tag;
					try {
						tag = asnReader.PeekTag();
					} catch (AsnContentException) {
						break;
					}

					switch (tag.TagValue) {
						case 2: // dNSName [2] IA5String
							sans.Add((asnReader.ReadCharacterString(UniversalTagNumber.IA5String, tag), CertificateNameType.DnsName));
							break;
						case 7: // iPAddress [7] OCTET STRING
							sans.Add((new IPAddress(asnReader.ReadOctetString(tag)).ToString(), CertificateNameType.IpAddress));
							break;
						default:
							asnReader.ReadEncodedValue();
							break;
					}
				}
			}

			return sans;
		}

		public static bool MatchesName(this X509Certificate certificate, string name) {
			// Implemented based on RFC 6125 (https://datatracker.ietf.org/doc/html/rfc6125) with the following changes:
			// - Does not support SRV-ID and URI-ID identifier types yet
			// - Partial wildcard support is not implemented since it has been deprecated in most major browsers

			if (certificate is not X509Certificate2 certificate2) {
				using var c2 = new X509Certificate2(certificate);
				certificate2 = c2;
			}

			var sans = GetSubjectAlternativeNames(certificate2).ToArray();
			if (sans.Length > 0)
				return sans.Any(san => MatchesName(san.name, san.type, name));

			var cn = GetCommonName(certificate2);
			return cn != null && MatchesName(cn, CertificateNameType.DnsName, name);
		}

		public static string GetCommonName(this X509Certificate2 certificate) => certificate.GetNameInfo(X509NameType.SimpleName, false);

		// FIPS compliant PKCS #12 bundle creation
		public static byte[] ExportToPkcs12(this X509Certificate2 certificate, string password = null) {
			password ??= string.Empty;

			using var rsa = RSA.Create();
			rsa.ImportRSAPrivateKey(certificate.GetRSAPrivateKey()!.ExportRSAPrivateKey(), out _);
			var builder = new Pkcs12Builder();
			var safeContents = new Pkcs12SafeContents();
			var pbeParams = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 2048);
			safeContents.AddCertificate(certificate);
			safeContents.AddShroudedKey(rsa, password, pbeParams);
			builder.AddSafeContentsEncrypted(safeContents, password, pbeParams);
			builder.SealWithMac(password, HashAlgorithmName.SHA256, 2048);

			return builder.Encode();
		}

		private static bool HasNonAsciiChars(string s) => s.Any(t => t > 127);

		private static bool IsInternationalizedDomainNameLabel(string s) {
			const string ACEPrefix = "xn--";
			return HasNonAsciiChars(s) || s.StartsWith(ACEPrefix, StringComparison.OrdinalIgnoreCase);
		}

		// Based on RFC 952
		private static bool IsValidDnsNameLabel(string label) =>
			label.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '-');

		// Based on RFC 6125 (without support for partial wildcard DNS labels
		// since it has been deprecated by most major browsers)
		private static bool IsValidCertificateNameFirstLabel(string label) =>
			label == "*" || IsValidDnsNameLabel(label);

		private static bool MatchesName(string certName, string certNameType, string name) {
			const char Wildcard = '*';
			const char Delimiter = '.';

			if (string.IsNullOrEmpty(certName) ||
			    string.IsNullOrEmpty(name))
				return false;

			// if at least one of the names is an IP address, do an exact match
			if (certNameType == CertificateNameType.IpAddress ||
			    IPAddress.TryParse(certName, out _) ||
			    IPAddress.TryParse(name, out _))
				return name.EqualsOrdinalIgnoreCase(certName);

			Debug.Assert(certNameType == CertificateNameType.DnsName);

			var certNameLabels = certName.Split(Delimiter);
			var dnsNameLabels = name.Split(Delimiter);

			if (certNameLabels.Length != dnsNameLabels.Length)
				return false;

			if (certNameLabels.Any(string.IsNullOrEmpty) ||
			    dnsNameLabels.Any(string.IsNullOrEmpty))
				return false;

			if (certNameLabels.Any(IsInternationalizedDomainNameLabel) ||
			    dnsNameLabels.Any(IsInternationalizedDomainNameLabel)) {
				var idnMapping = new IdnMapping();
				dnsNameLabels = dnsNameLabels.Select(x => idnMapping.GetAscii(x)).ToArray();
				certNameLabels = certNameLabels.Select(x => idnMapping.GetAscii(x)).ToArray();
			}

			if (!IsValidCertificateNameFirstLabel(certNameLabels.First()) ||
			    !certNameLabels.Skip(1).All(IsValidDnsNameLabel) ||
			    !dnsNameLabels.All(IsValidDnsNameLabel))
				return false;

			// if first label is not a wildcard, check for an exact match
			if (certNameLabels.First() != Wildcard.ToString())
				return certNameLabels.EqualsOrdinalIgnoreCase(dnsNameLabels);

			// first label is wildcard, compare the other labels
			return certNameLabels.Skip(1).EqualsOrdinalIgnoreCase(dnsNameLabels.Skip(1));
		}
	}
}
