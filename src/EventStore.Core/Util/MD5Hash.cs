// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.Collections.Generic;
using EventStore.Common.Utils;
using MD5 = EventStore.Core.Hashing.MD5;

namespace EventStore.Core.Util;

public class MD5Hash {
	public static byte[] GetHashFor(Stream s) {
		//when using this, it will calculate from this point to the END of the stream!
		using (var md5 = MD5.Create())
			return md5.ComputeHash(s);
	}

	public static byte[] GetHashFor(Stream s, int startPosition, long count) {
		Ensure.Nonnegative(count, "count");

		using (var md5 = MD5.Create()) {
			ContinuousHashFor(md5, s, startPosition, count);
			md5.TransformFinalBlock(Empty.ByteArray, 0, 0);
			return md5.Hash;
		}
	}

	public static void ContinuousHashFor(HashAlgorithm md5, Stream s, int startPosition, long count) {
		Ensure.NotNull(md5, "md5");
		Ensure.Nonnegative(count, "count");

		if (s.Position != startPosition)
			s.Position = startPosition;

		var buffer = new byte[4096];
		long toRead = count;
		while (toRead > 0) {
			int read = s.Read(buffer, 0, (int)Math.Min(toRead, buffer.Length));
			if (read == 0)
				break;

			md5.TransformBlock(buffer, 0, read, null, 0);
			toRead -= read;
		}
	}

	public static async ValueTask ContinuousHashFor(HashAlgorithm md5, Stream s, int startPosition, long count, CancellationToken token) {
		Ensure.NotNull(md5, "md5");
		Ensure.Nonnegative(count, "count");

		if (s.Position != startPosition)
			s.Position = startPosition;

		var buffer = ArrayPool<byte>.Shared.Rent(4096);
		try {
			long toRead = count;
			while (toRead > 0) {
				int read = await s.ReadAsync(buffer.AsMemory(0, (int)Math.Min(toRead, buffer.Length)), token);
				if (read is 0)
					break;

				md5.TransformBlock(buffer, 0, read, null, 0);
				toRead -= read;
			}
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}
