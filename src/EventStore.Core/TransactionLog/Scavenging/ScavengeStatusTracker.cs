// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Metrics;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IScavengeStatusTracker {
		IDisposable StartActivity(string name);
	}

	public class ScavengeStatusTracker : IScavengeStatusTracker {
		private static ActivityStatusSubMetric _subMetric;

		public ScavengeStatusTracker(StatusMetric metric) {
			_subMetric = new("Scavenge", metric);
		}

		public IDisposable StartActivity(string name) =>
			_subMetric?.StartActivity(name + " Phase");

		public class NoOp : IScavengeStatusTracker {
			public IDisposable StartActivity(string name) => null;
		}
	}
}
