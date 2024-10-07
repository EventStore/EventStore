// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.LogAbstraction;

/// Maps names (strings) to TValues
public interface INameIndex<TValue> {
	void CancelReservations();

	// return true => stream already existed.
	// return false => stream was created. addedValue and addedName are the details of the created stream.
	// these can be different to streamName/streamId e.g. if streamName is a metastream.
	bool GetOrReserve(string name, out TValue value, out TValue addedValue, out string addedName);
}
