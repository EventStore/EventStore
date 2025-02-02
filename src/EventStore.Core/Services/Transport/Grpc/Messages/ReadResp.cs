// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

// ReSharper disable CheckNamespace
namespace EventStore.Client.Streams;

// ReSharper restore CheckNamespace
partial class ReadResp {
	partial class Types {
		partial class ReadEvent {
			public Types.RecordedEvent OriginalEvent => Link ?? Event;
		}
	}
}
