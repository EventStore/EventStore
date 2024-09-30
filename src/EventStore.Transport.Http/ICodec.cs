// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text;

namespace EventStore.Transport.Http {
	public interface ICodec {
		string ContentType { get; }
		Encoding Encoding { get; }
		bool CanParse(MediaType format);
		bool SuitableForResponse(MediaType component);
		bool HasEventIds { get; }
		bool HasEventTypes { get; }
		T From<T>(string text);
		string To<T>(T value);
	}
}
