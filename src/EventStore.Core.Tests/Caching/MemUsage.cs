// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Tests.Caching {
	public class MemUsage {
		public static long Calculate<T>(Func<T> createObject, out T newObject) {
			var memBefore = GC.GetAllocatedBytesForCurrentThread();
			newObject = createObject();
			var memAfter = GC.GetAllocatedBytesForCurrentThread();

			return memAfter - memBefore;
		}

		public static long Calculate(Action createObject) {
			return Calculate<object>(() => {
				createObject();
				return null;
			}, out _);
		}
	}
}
