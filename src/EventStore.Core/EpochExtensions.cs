// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core {
	internal static class EpochExtensions {
		public static DateTime FromTicksSinceEpoch(this long value) =>
			new DateTime(DateTime.UnixEpoch.Ticks + value, DateTimeKind.Utc);

		public static long ToTicksSinceEpoch(this DateTime value) =>
			(value - DateTime.UnixEpoch).Ticks;
	}
}
