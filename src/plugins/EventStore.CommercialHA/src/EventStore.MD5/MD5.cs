// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace EventStore.MD5;

internal class MD5 : HashAlgorithm {
	public new static MD5 Create() => new();

	private static int[] s = {
		7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,
		5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,
		4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,
		6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21
	};

	private static uint[] K = {
		0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee,
		0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
		0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be,
		0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
		0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa,
		0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
		0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed,
		0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
		0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c,
		0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
		0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05,
		0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
		0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039,
		0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
		0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1,
		0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391
	};

	// precomputed values for g:
	// if   i < 16: g = i
	// elif i < 32: g = (5 * i + 1) mod 16
	// elif i < 48: g = (3 * i + 5) mod 16
	// elif i < 64: g = (7 * i) mod 16
	private static byte[] g = {
		0, 1,  2,  3,   4,  5,  6,  7,   8,  9, 10, 11,  12, 13, 14, 15,
		1, 6, 11,  0,   5, 10, 15,  4,   9, 14,  3,  8,  13,  2,  7, 12,
		5, 8, 11, 14,   1,  4,  7, 10,  13,  0,  3,  6,   9, 12, 15,  2,
		0, 7, 14,  5,  12,  3, 10,  1,   8, 15,  6, 13,   4, 11,  2,  9
	};

	private uint _a, _b, _c, _d;

	private const int BlockSize = 64;
	private readonly byte[] _buffer;
	private int _bufferPos;

	private const int MLen = 16;
	private readonly uint[] M;

	private ulong _msgLen;

	private bool _disposed;

	private MD5() {
		if (!BitConverter.IsLittleEndian)
			throw new NotSupportedException();

		HashSizeValue = 128;

		_buffer = ArrayPool<byte>.Shared.Rent(BlockSize);
		M = ArrayPool<uint>.Shared.Rent(MLen);

		ResetState();
	}

	private void ResetState() {
		_a = 0x67452301;
		_b = 0xefcdab89;
		_c = 0x98badcfe;
		_d = 0x10325476;
		_bufferPos = 0;
		_msgLen = 0;
	}

	public override void Initialize() => ResetState();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessBuffer(ReadOnlySpan<byte> buffer) {
		Debug.Assert(buffer.Length == BlockSize);

		for (var k = 0; k < MLen; k ++)
			M[k] = BitConverter.ToUInt32(buffer[(4 * k)..(4 * (k + 1))]);

		uint a = _a, b = _b, c = _c, d = _d;

		// the loop has been unrolled since it makes better use of the processor pipeline
		// and thus offers much better performance. the key reason is that the next line can
		// already be partially calculated even if calculation of the previous line hasn't
		// yet completed.

		var i = 0;
		// round 1
		a = LeftRotate(((b & c) | (~b & d)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((a & b) | (~a & c)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((d & a) | (~d & b)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((c & d) | (~c & a)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate(((b & c) | (~b & d)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((a & b) | (~a & c)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((d & a) | (~d & b)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((c & d) | (~c & a)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate(((b & c) | (~b & d)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((a & b) | (~a & c)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((d & a) | (~d & b)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((c & d) | (~c & a)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate(((b & c) | (~b & d)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((a & b) | (~a & c)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((d & a) | (~d & b)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((c & d) | (~c & a)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		// round 2
		a = LeftRotate(((d & b) | (~d & c)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((c & a) | (~c & b)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((b & d) | (~b & a)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((a & c) | (~a & d)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate(((d & b) | (~d & c)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((c & a) | (~c & b)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((b & d) | (~b & a)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((a & c) | (~a & d)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate(((d & b) | (~d & c)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((c & a) | (~c & b)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((b & d) | (~b & a)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((a & c) | (~a & d)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate(((d & b) | (~d & c)) + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate(((c & a) | (~c & b)) + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate(((b & d) | (~b & a)) + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate(((a & c) | (~a & d)) + b + K[i] + M[g[i]], s[i]) + c; ++i;

		// round 3
		a = LeftRotate((b ^ c ^ d)          + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((a ^ b ^ c)          + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((d ^ a ^ b)          + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((c ^ d ^ a)          + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate((b ^ c ^ d)          + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((a ^ b ^ c)          + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((d ^ a ^ b)          + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((c ^ d ^ a)          + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate((b ^ c ^ d)          + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((a ^ b ^ c)          + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((d ^ a ^ b)          + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((c ^ d ^ a)          + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate((b ^ c ^ d)          + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((a ^ b ^ c)          + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((d ^ a ^ b)          + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((c ^ d ^ a)          + b + K[i] + M[g[i]], s[i]) + c; ++i;

		// round 4
		a = LeftRotate((c ^ (b | ~d))       + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((b ^ (a | ~c))       + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((a ^ (d | ~b))       + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((d ^ (c | ~a))       + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate((c ^ (b | ~d))       + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((b ^ (a | ~c))       + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((a ^ (d | ~b))       + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((d ^ (c | ~a))       + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate((c ^ (b | ~d))       + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((b ^ (a | ~c))       + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((a ^ (d | ~b))       + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((d ^ (c | ~a))       + b + K[i] + M[g[i]], s[i]) + c; ++i;

		a = LeftRotate((c ^ (b | ~d))       + a + K[i] + M[g[i]], s[i]) + b; ++i;
		d = LeftRotate((b ^ (a | ~c))       + d + K[i] + M[g[i]], s[i]) + a; ++i;
		c = LeftRotate((a ^ (d | ~b))       + c + K[i] + M[g[i]], s[i]) + d; ++i;
		b = LeftRotate((d ^ (c | ~a))       + b + K[i] + M[g[i]], s[i]) + c; ++i;

		_a += a; _b += b; _c += c; _d += d;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint LeftRotate(uint num, int shift) =>
		(num << shift) | (num >> (32 - shift));

	protected override void HashCore(byte[] src, int srcOffset, int srcCnt) {
		Debug.Assert(srcOffset + srcCnt <= src.Length);

		_msgLen += (ulong) srcCnt * 8;

		while (srcCnt > 0) {
			var cnt = Math.Min(BlockSize - _bufferPos, srcCnt);

			if (cnt == BlockSize) {
				// process directly, without copying, if it's a whole block
				ProcessBuffer(src.AsSpan()[srcOffset..(srcOffset + BlockSize)]);
			} else {
				// otherwise, fill up the buffer and process it if it's full
				Buffer.BlockCopy(src, srcOffset, _buffer, _bufferPos, cnt);
				_bufferPos += cnt;
				if (_bufferPos == BlockSize) {
					ProcessBuffer(_buffer.AsSpan()[..BlockSize]);
					_bufferPos = 0;
				}
			}

			srcOffset += cnt;
			srcCnt -= cnt;
		}
	}

	protected override byte[] HashFinal() {
		_buffer[_bufferPos++] = 1 << 7;
		_bufferPos %= BlockSize;

		while (_bufferPos != BlockSize - 8) {
			_buffer[_bufferPos++] = 0;
			_bufferPos %= BlockSize;
		}

		BitConverter.TryWriteBytes(_buffer.AsSpan()[_bufferPos..BlockSize], _msgLen);
		_bufferPos += 8;

		Debug.Assert(_bufferPos == BlockSize);
		ProcessBuffer(_buffer.AsSpan()[..BlockSize]);
		_bufferPos = 0;

		var res = new byte[16];
		var resSpan = res.AsSpan();

		BitConverter.TryWriteBytes(resSpan[..4], _a);
		BitConverter.TryWriteBytes(resSpan[4..8], _b);
		BitConverter.TryWriteBytes(resSpan[8..12], _c);
		BitConverter.TryWriteBytes(resSpan[12..16], _d);

		return res;
	}

	protected override void Dispose(bool disposing) {
		if (!_disposed) {
			if (disposing) {
				ArrayPool<byte>.Shared.Return(_buffer);
				ArrayPool<uint>.Shared.Return(M);
			}

			_disposed = true;
		}

		base.Dispose(disposing);
	}
}
