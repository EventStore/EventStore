// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;

namespace EventStore.Transport.Tcp;

internal static class Helper {
	public static void EatException(Action action) {
		try {
			action();
		} catch (Exception) {
		}
	}

	public static T EatException<T>(Func<T> func, T defaultValue = default(T)) {
		Ensure.NotNull(func, "func");
		try {
			return func();
		} catch (Exception) {
			return defaultValue;
		}
	}
}
