// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public static class ArrayExtensions {
	// keep records at the specified indexes in the chunk
	public static T[] KeepIndexes<T>(this T[] self, params int[] indexes) {
		foreach (var i in indexes) {
			Assert.True(i < self.Length, $"error in test: index {i} does not exist");
		}

		return self.Where((x, i) => indexes.Contains(i)).ToArray();
	}

	public static T[] KeepNone<T>(this T[] _) => [];

	public static T[] KeepAll<T>(this T[] self) => self;
}
