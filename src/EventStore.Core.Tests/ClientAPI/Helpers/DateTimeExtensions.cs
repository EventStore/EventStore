using System;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public static class DateTimeExtensions {
	public static long ToEpoch(this DateTime dateTime) {
		var utc = dateTime.ToUniversalTime();
		var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return (long)(utc - epochStart).TotalSeconds;
	}
}
