// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Utils {
	public static class EncodingExtensions {
		public static string FromUtf8(this byte[] self) {
			return Helper.UTF8NoBom.GetString(self);
		}

		public static string FromUtf8(this ReadOnlyMemory<byte> self) {
			return Helper.UTF8NoBom.GetString(self.Span);
		}

		public static byte[] ToUtf8(this string self) {
			return Helper.UTF8NoBom.GetBytes(self);
		}

		public static string Apply(this string format, params object[] args) {
			return string.Format(format, args);
		}
	}
}
