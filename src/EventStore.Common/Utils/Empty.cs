// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Common.Utils {
	public static class Empty {
		public static readonly byte[] ByteArray = new byte[0];
		public static readonly string[] StringArray = new string[0];
		public static readonly object[] ObjectArray = new object[0];

		public static readonly Action Action = () => { };
		public static readonly object Result = new object();
		public static readonly string Xml = String.Empty;
		public static readonly string Json = "{}";
	}
}
