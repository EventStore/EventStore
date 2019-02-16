using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI.Internal {
	internal static class Empty {
		public static readonly byte[] ByteArray = new byte[0];
		public static readonly string[] StringArray = new string[0];
		public static readonly ResolvedEvent[] ResolvedEvents = new ResolvedEvent[0];

		public static readonly Action Action = () => { };

		public static readonly IDictionary<string, JToken> CustomStreamMetadata = new Dictionary<string, JToken>();
	}
}
