using System;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static class DateTimeExtensions {
		public static long ToTicksSinceEpoch(this DateTime value) => (value - DateTime.UnixEpoch).Ticks;
	}
}
