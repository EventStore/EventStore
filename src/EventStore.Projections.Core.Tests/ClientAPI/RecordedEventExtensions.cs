// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text;
using EventStore.ClientAPI;

namespace EventStore.Projections.Core.Tests.ClientAPI {
	internal static class RecordedEventExtensions {
		public static string DebugDataView(this RecordedEvent source) => Encoding.UTF8.GetString(source.Data);
		public static string DebugMetadataView(this RecordedEvent source) => Encoding.UTF8.GetString(source.Metadata);
	}
}
