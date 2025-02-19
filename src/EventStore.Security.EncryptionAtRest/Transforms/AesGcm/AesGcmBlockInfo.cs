// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

public static class AesGcmBlockInfo {
	public const int BlockSize = 512;
	public const int HeaderSize = 2;
	public const int DataSize = BlockSize - HeaderSize - TagSize;
	public const int TagSize = 14; // so that `DataSize` is aligned to the AES block size: 16 bytes
}
