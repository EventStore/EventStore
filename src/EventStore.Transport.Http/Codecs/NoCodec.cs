// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text;

namespace EventStore.Transport.Http.Codecs;

public class NoCodec : ICodec {
	public string ContentType {
		get { throw new NotSupportedException(); }
	}

	public Encoding Encoding {
		get { throw new NotSupportedException(); }
	}

	public bool HasEventIds {
		get { return false; }
	}

	public bool HasEventTypes {
		get { return false; }
	}

	public bool CanParse(MediaType format) {
		return false;
	}

	public bool SuitableForResponse(MediaType component) {
		return false;
	}

	public T From<T>(string text) {
		throw new NotSupportedException();
	}

	public string To<T>(T value) {
		throw new NotSupportedException();
	}
}
