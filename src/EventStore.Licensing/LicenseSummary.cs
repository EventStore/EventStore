// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EventStore.Plugins.Licensing;

namespace EventStore.Licensing;

public record LicenseSummary(
	string LicenseId,
	string Company,
	bool IsTrial,
	bool IsExpired,
	bool IsValid,
	bool IsFloating,
	int DaysRemaining,
	long StartDate,
	string Notes) {

	public LicenseSummary(string licenseId, string company, bool isTrial, bool isExpired, bool isValid, bool isFloating, int daysRemaining, DateTime startDate, string notes)
		: this(licenseId, company, isTrial, isExpired, isValid, isFloating, daysRemaining, (long)(startDate - DateTime.UnixEpoch).TotalSeconds, notes) {
	}

	public void Export(in Dictionary<string, object> props) {
		foreach (var property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
			props.Add(ToCamelCase(property.Name), property.GetValue(this)!);
	}

	public static HashSet<string> Properties { get; } =
		typeof(LicenseSummary).GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(p => ToCamelCase(p.Name))
			.ToHashSet();

	public static Dictionary<string, object?> SelectForEndpoint(License license) {
		var dict = new Dictionary<string, object?>();

		foreach (var claim in license.Token.Claims ?? []) {
			if (Properties.Contains(claim.Type)) {
				dict[claim.Type] = claim.Value;
			}
		}

		return dict;
	}

	public static Dictionary<string, object?> SelectForTelemetry(License license) {
		var dict = new Dictionary<string, object?>();

		AddString(nameof(LicenseId), license, dict);
		AddBool(nameof(IsTrial), license, dict);
		AddBool(nameof(IsExpired), license, dict);
		AddBool(nameof(IsValid), license, dict);

		static void AddString(string property, License license, Dictionary<string, object?> dict) {
			if (TryGet(property, license, out var k, out var v))
				dict[k] = v;
		}

		static void AddBool(string property, License license, Dictionary<string, object?> dict) {
			if (TryGet(property, license, out var k, out var v) && bool.TryParse(v, out var b))
				dict[k] = b;
		}

		static bool TryGet(string property, License l, out string key, [MaybeNullWhen(false)] out string value) {
			key = ToCamelCase(property);
			foreach (var claim in l.Token.Claims ?? []) {
				if (key == claim.Type) {
					value = claim.Value;
					return true;
				}
			}
			value = default;
			return false;
		};

		return dict;
	}

	private static string ToCamelCase(string pascalCaseStr) {
		if (string.IsNullOrEmpty(pascalCaseStr))
			return pascalCaseStr;

		return char.ToLower(pascalCaseStr[0]) + pascalCaseStr[1..];
	}
}
