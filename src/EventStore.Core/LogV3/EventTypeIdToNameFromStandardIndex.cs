// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV3 {
	public class EventTypeIdToNameFromStandardIndex : INameLookup<uint> {
		private readonly IIndexReader<uint> _indexReader;

		public EventTypeIdToNameFromStandardIndex(IIndexReader<uint> indexReader) {
			_indexReader = indexReader;
		}

		public bool TryGetName(uint eventTypeId, out string name) {
			var record = _indexReader.ReadPrepare(
				streamId: LogV3SystemStreams.EventTypesStreamNumber,
				eventNumber: EventTypeIdConverter.ToEventNumber(eventTypeId));

			if (record is null) {
				name = null;
				return false;
			}

			if (record is not LogV3EventTypeRecord eventTypeRecord)
				throw new Exception($"Unexpected log record type: {record}.");

			name = eventTypeRecord.EventTypeName;
			return true;
		}

		public bool TryGetLastValue(out uint lastValue) {
			var lastEventNumber = _indexReader.GetStreamLastEventNumber(LogV3SystemStreams.EventTypesStreamNumber);
			var success = ExpectedVersion.NoStream < lastEventNumber && lastEventNumber != EventNumber.DeletedStream;
			lastValue = EventTypeIdConverter.ToEventTypeId(lastEventNumber);
			return success;
		}
	}
}
