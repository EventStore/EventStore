// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	internal class EventsStream {
		private const int SliceSize = 10;

		public static async Task<int> Count(IEventStoreConnection store, string stream) {
			var result = 0;
			while (true) {
				var slice = await store.ReadStreamEventsForwardAsync(stream, result, SliceSize, false);
				result += slice.Events.Length;
				if (slice.IsEndOfStream)
					break;
			}

			return result;
		}
	}
}
