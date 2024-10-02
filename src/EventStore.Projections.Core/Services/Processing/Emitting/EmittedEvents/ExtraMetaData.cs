// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents {
	public class ExtraMetaData {
		private readonly Dictionary<string, string> _metadata;

		public ExtraMetaData(Dictionary<string, JRaw> metadata) {
			_metadata = metadata.ToDictionary(v => v.Key, v => v.Value.ToString());
		}

		public ExtraMetaData(Dictionary<string, string> metadata) {
			_metadata = metadata.ToDictionary(v => v.Key, v => v.Value);
		}

		public Dictionary<string, string> Metadata {
			get { return _metadata; }
		}
	}
}
