using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using static EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core.Telemetry;

public class CacheHitsMissesMetric {
	private readonly Cache[] _enabledCaches;
	private readonly List<Func<long>> _funcs = new();
	private readonly List<KeyValuePair<string, object>[]> _tagss = new();
	private readonly object _lock = new();

	public CacheHitsMissesMetric(Meter meter, string name, Cache[] enabledCaches) {
		_enabledCaches = enabledCaches;
		meter.CreateObservableCounter(name, Observe);
	}

	public void Register(Cache cache, Func<long> getHits, Func<long> getMisses) {
		Register(cache, "hits", getHits);
		Register(cache, "misses", getMisses);
	}

	private void Register(Cache cache, string kind, Func<long> func) {
		if (!_enabledCaches.Contains(cache))
			return;

		lock (_lock) {
			_funcs.Add(func);
			_tagss.Add(new KeyValuePair<string, object>[] {
				new("cache", KebabCase(cache)),
				new("kind", kind),
			});
		}
	}

	private static string KebabCase(Cache cache) => cache switch {
		Cache.StreamInfo => "stream-info",
		Cache.Chunk => "chunk",
		_ => throw new ArgumentOutOfRangeException(nameof(cache), cache, null),
	};

	private IEnumerable<Measurement<long>> Observe() {
		lock (_lock) {
			for (var i = 0; i < _funcs.Count; i++) {
				yield return new(_funcs[i](), _tagss[i]);
			}
		}
	}
}
