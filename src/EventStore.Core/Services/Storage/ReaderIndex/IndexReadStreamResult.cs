// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex;

public readonly struct IndexReadStreamResult {
	internal static readonly EventRecord[] EmptyRecords = [];

	public readonly long FromEventNumber;
	public readonly int MaxCount;

	public readonly ReadStreamResult Result;
	public readonly long NextEventNumber;
	public readonly long LastEventNumber;
	public readonly bool IsEndOfStream;

	public readonly EventRecord[] Records;
	public readonly StreamMetadata Metadata;

	/// <summary>
	/// Failure Constructor
	/// </summary>
	public IndexReadStreamResult(long fromEventNumber, int maxCount, ReadStreamResult result, StreamMetadata metadata, long lastEventNumber) {
		if (result == ReadStreamResult.Success)
			throw new ArgumentException($"Wrong ReadStreamResult provided for failure constructor: {result}.", nameof(result));

		FromEventNumber = fromEventNumber;
		MaxCount = maxCount;

		Result = result;
		NextEventNumber = -1;
		LastEventNumber = lastEventNumber;
		IsEndOfStream = true;
		Records = EmptyRecords;
		Metadata = metadata;
	}

	/// <summary>
	/// Success Constructor
	/// </summary>
	/// <param name="fromEventNumber">
	///     EventNumber that the read starts from. Usually as specified in the request.
	///     Sometimes resolved (e.g. from EndOfStream to the actual lastEventNumber)
	///     This is not returned to the client when reading forwards, only backwards.
	/// </param>
	/// <param name="maxCount">The maximum number of events requested</param>
	/// <param name="records"></param>
	/// <param name="metadata"></param>
	/// <param name="nextEventNumber">The event number to start the next _request_</param>
	/// <param name="lastEventNumber">The last event number of the _stream_</param>
	/// <param name="isEndOfStream"></param>
	public IndexReadStreamResult(
		long fromEventNumber,
		int maxCount,
		EventRecord[] records,
		StreamMetadata metadata,
		long nextEventNumber,
		long lastEventNumber,
		bool isEndOfStream) {
		Ensure.NotNull(records);

		FromEventNumber = fromEventNumber;
		MaxCount = maxCount;

		Result = ReadStreamResult.Success;
		Records = records;
		Metadata = metadata;
		NextEventNumber = nextEventNumber;
		LastEventNumber = lastEventNumber;
		IsEndOfStream = isEndOfStream;
	}

	public override string ToString() {
		return $"FromEventNumber: {FromEventNumber}, Maxcount: {MaxCount}, Result: {Result}, Record count: {Records.Length}, Metadata: {Metadata}, " +
		       $"NextEventNumber: {NextEventNumber}, LastEventNumber: {LastEventNumber}, IsEndOfStream: {IsEndOfStream}";
	}
}
