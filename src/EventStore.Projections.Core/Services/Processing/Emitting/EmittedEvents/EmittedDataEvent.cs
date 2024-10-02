// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents {
	public class EmittedDataEvent : EmittedEvent {
		private readonly string _data;
		private readonly ExtraMetaData _metadata;
		private readonly bool _isJson;

		public EmittedDataEvent(
			string streamId, Guid eventId,
			string eventType, bool isJson, string data, ExtraMetaData metadata, CheckpointTag causedByTag,
			CheckpointTag expectedTag,
			Action<long> onCommitted = null)
			: base(streamId, eventId, eventType, causedByTag, expectedTag, onCommitted) {
			_isJson = isJson;
			_data = data;
			_metadata = metadata;
		}

		public override string Data {
			get { return _data; }
		}

		public ExtraMetaData Metadata {
			get { return _metadata; }
		}

		public override bool IsJson {
			get { return _isJson; }
		}

		public override bool IsReady() {
			return true;
		}

		public override IEnumerable<KeyValuePair<string, string>> ExtraMetaData() {
			return _metadata == null ? null : _metadata.Metadata;
		}

		public override string ToString() {
			return string.Format("Event Id: {0}, Event Type: {1}, Data: {2}", EventId, EventType, Data);
		}
	}
}
