using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing {
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
