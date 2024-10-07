// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Common.Utils;

public static class EnumerableExtensions {
	public static IEnumerable<T> Safe<T>(this IEnumerable<T> collection) {
		return collection ?? Enumerable.Empty<T>();
	}

	public static bool Contains<T>(this IEnumerable<T> collection, Predicate<T> condition) {
		return collection.Any(x => condition(x));
	}

	public static bool IsEmpty<T>(this IEnumerable<T> collection) {
		if (collection == null)
			return true;
		var coll = collection as ICollection;
		if (coll != null)
			return coll.Count == 0;
		return !collection.Any();
	}

	public static bool IsNotEmpty<T>(this IEnumerable<T> collection) {
		return !IsEmpty(collection);
	}
}
