using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Serilog;
using Conf = EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core.Telemetry;

public class QueueTrackers {
	private static readonly ILogger Log = Serilog.Log.ForContext<QueueTrackers>();

	private readonly Dictionary<string, QueueTracker> _trackers = new();
	private readonly QueueTracker _noOp = QueueTracker.NoOp;
	private readonly Conf.LabelMappingCase[] _cases;
	private readonly Func<string, QueueTracker> _trackerFactory;

	public QueueTrackers() {
		_cases = Array.Empty<Conf.LabelMappingCase>();
		_trackerFactory = _ => _noOp;
	}

	public QueueTrackers(
		Conf.LabelMappingCase[] cases,
		Func<string, QueueTracker> trackerFactory) {

		_cases = cases;
		_trackerFactory = trackerFactory;
	}

	public QueueTracker GetTrackerForQueue(string queueName) {
		foreach (var @case in _cases) {
			var pattern = $"^{@case.Regex}$";
			var match = Regex.Match(input: queueName, pattern: pattern);
			if (match.Success) {
				var label = Regex.Replace(
					input: queueName,
					pattern: pattern,
					replacement: @case.Label);

				if (string.IsNullOrWhiteSpace(label))
					return _noOp;

				Log.Information(
					"Telemetry matched queue {queueName} with pattern {pattern}. Label: {label}",
					queueName, @case.Regex, label);

				return GetTrackerByName(label);
			}
		}

		Log.Information("Telemetry did not match queue {queueName}. Metrics will not be collected", queueName);
		return _noOp;
	}

	private QueueTracker GetTrackerByName(string trackerName) {
		if (!_trackers.TryGetValue(trackerName, out var tracker)) {
			tracker = _trackerFactory(trackerName);
			_trackers[trackerName] = tracker;
		}

		return tracker;
	}
}
