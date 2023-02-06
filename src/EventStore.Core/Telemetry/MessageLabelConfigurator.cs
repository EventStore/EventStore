using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using EventStore.Common.Configuration;
using Serilog;

namespace EventStore.Core.Telemetry;

public class MessageLabelConfigurator {
	private static readonly ILogger Log = Serilog.Log.ForContext<MessageLabelConfigurator>();

	public static void ConfigureMessageLabels(
		TelemetryConfiguration.LabelMappingCase[] configuration,
		IEnumerable<Type> messageTypes) {

		var labels = new HashSet<string>();

		foreach (var messageType in messageTypes) {
			if (TryConfigureMessageType(configuration, messageType, out var label)) {
				labels.Add(label);
			}
		}

		Log.Information("Telemetry created {count} message type labels", labels.Count);
	}

	private static bool TryConfigureMessageType(
		TelemetryConfiguration.LabelMappingCase[] configuration,
		Type messageType,
		out string label) {

		label = default;

		if (messageType.IsAbstract)
			return false;

		var labelStaticProperty = messageType
			.GetProperty("LabelStatic", BindingFlags.Static | BindingFlags.Public);

		if (labelStaticProperty is null) {
			Log.Warning($"{messageType} may be missing the DerivedMessage attribute.");
			return false;
		}

		if (labelStaticProperty.GetValue(null) is not string oldLabel) {
			oldLabel = "";
		}


		foreach (var @case in configuration) {
			var pattern = $"^{@case.Regex}$";
			var match = Regex.Match(input: oldLabel, pattern: pattern);
			if (match.Success) {
				label = Regex.Replace(
					input: oldLabel,
					pattern: pattern,
					replacement: @case.Label);

				labelStaticProperty.SetValue(null, label);

				Log.Verbose(
					"Telemetry matched message {old} with pattern {pattern} and set it to {new}",
					oldLabel, @case.Regex, label);

				return true;
			}
		}

		label = oldLabel;
		return true;
	}
}
