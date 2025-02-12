// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine;

public static class IAsyncEnumerableExtenions {
	public static IAsyncEnumerable<Message> DeserializeWithDefault(
		this IAsyncEnumerable<Event> self,
		ISerializer serializer,
		Message unknown) =>
		
		self.Select(x => {
			if (!serializer.TryDeserialize(x, out var message, out _)) {
				message = unknown;
			}
			return message!;
		});

	public static IAsyncEnumerable<Message> DeserializeSkippingUnknown(
		this IAsyncEnumerable<Event> self,
		ISerializer serializer) =>
		
		self
			.Select(x => serializer.TryDeserialize(x, out var message, out _)
				? message
				: null)
			.Where(x => x is not null)
			.Select(x => x!);
}
