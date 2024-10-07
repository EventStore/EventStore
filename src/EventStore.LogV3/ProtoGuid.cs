// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Google.Protobuf;

namespace EventStore.LogV3;

// System.Guid.ToByteArray roundtrips with the ctor that takes byte[] as the argument
// if changing this be careful not to break changing between endianness
public partial class ProtoGuid {
	public static implicit operator Guid(ProtoGuid x) {
		if (x == null)
			return default;

		return new Guid(x.Bytes.ToByteArray());
	}

	public static implicit operator ProtoGuid(Guid x) {
		return new ProtoGuid {
			Bytes = ByteString.CopyFrom(x.ToByteArray()),
		};
	}
}
