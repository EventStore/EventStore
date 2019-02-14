using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EventStore.Projections.Core.Services.Processing {
	public class EmittedLinkToWithRecategorization : EmittedEvent {
		private readonly string _target;
		private readonly string _originalStreamId;
		private readonly int? _streamDeletedAt;

		public EmittedLinkToWithRecategorization(
			string streamId, Guid eventId, string target, CheckpointTag causedByTag, CheckpointTag expectedTag,
			string originalStreamId, int? streamDeletedAt)
			: base(streamId, eventId, "$>", causedByTag, expectedTag, null) {
			_target = target;
			_originalStreamId = originalStreamId;
			_streamDeletedAt = streamDeletedAt;
		}

		public override string Data {
			get { return _target; }
		}

		public override bool IsJson {
			get { return false; }
		}

		public override bool IsReady() {
			return true;
		}

		public override IEnumerable<KeyValuePair<string, string>> ExtraMetaData() {
			if (!string.IsNullOrEmpty(_originalStreamId))
				yield return new KeyValuePair<string, string>("$o", JsonConvert.ToString(_originalStreamId));
			if (_streamDeletedAt != null)
				yield return new KeyValuePair<string, string>("$deleted", JsonConvert.ToString(_streamDeletedAt.Value));
		}
	}
}
