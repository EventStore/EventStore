// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex;

public struct IndexReadEventResult {
	public readonly ReadEventResult Result;
	public readonly EventRecord Record;
	public readonly StreamMetadata Metadata;
	public readonly long LastEventNumber;
	public readonly bool? OriginalStreamExists;

	public IndexReadEventResult(ReadEventResult result, StreamMetadata metadata, long lastEventNumber,
		bool? originalStreamExists) {
		if (result == ReadEventResult.Success)
			throw new ArgumentException(
				string.Format("Wrong ReadEventResult provided for failure constructor: {0}.", result), "result");

		Result = result;
		Record = null;
		Metadata = metadata;
		LastEventNumber = lastEventNumber;
		OriginalStreamExists = originalStreamExists;
	}

	public IndexReadEventResult(ReadEventResult result, EventRecord record, StreamMetadata metadata,
		long lastEventNumber, bool? originalStreamExists) {
		Result = result;
		Record = record;
		Metadata = metadata;
		LastEventNumber = lastEventNumber;
		OriginalStreamExists = originalStreamExists;
	}
}
