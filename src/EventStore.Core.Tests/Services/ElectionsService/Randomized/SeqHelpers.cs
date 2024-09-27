// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	public static class SeqHelpers {
		public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Func<int, int> rndNext) {
			var array = source.ToArray();
			var n = array.Length;
			while (n > 1) {
				var k = rndNext(n);
				n--;
				var temp = array[n];
				array[n] = array[k];
				array[k] = temp;
			}

			return array;
		}
	}
}
