// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace EventStore.MD5.Tests;

public class MD5Tests : IAsyncLifetime {
	private HashAlgorithm? _md5;

	public Task InitializeAsync() {
		_md5 = MD5.Create();
		return Task.CompletedTask;
	}

	public Task DisposeAsync() {
		_md5!.Dispose();
		return Task.CompletedTask;
	}

	[Fact]
	public void produces_correct_hash() {
		// mind the newline at the top of the file
		string md5Poem = @"
In the realm of bits and code,
Where secrets hide and paths corrode,
Lies an algorithm, silent and wise,
MD5, the guardian of digital ties.

Through the bytes, it weaves its spell,
With hashing power, it quells and compels,
A fingerprint of data, a digital crest,
MD5 dances, never to rest.

In realms of cryptography, caution we heed,
MD5's tale of vulnerability we shall read,
Once hailed as strong, now weakened by time,
A warning bell, ringing a cautionary chime.

MD5, a hash function of the past,
Its flaws exposed, vulnerabilities amassed,
Collisions arise, where paths intertwine,
A crack in the armor, a loophole we find.

For cryptographic strength, it may not suffice,
A realm where security demands a higher price,
As adversaries grow and threats evolve,
MD5's weakness, we must firmly resolve.

Yet in the realm of data integrity's embrace,
MD5's role finds a worthy place,
Checksums and verification, its domain well-fit,
Ensuring data's integrity, a task it can commit.

So let us wield it with care, in its rightful realm,
Data's watchdog, preserving its helm,
But for cryptography's secrets, we must seek anew,
Stronger hash functions, with resilience true.".Replace("\r\n", "\n");

		var data = Encoding.UTF8.GetBytes(md5Poem);
		Assert.Equal("9D72E67326E53E78F84B309A6E1DEE25", ComputeHash(data));
	}

	[Fact]
	public void produces_correct_hash_for_empty_data() {
		Assert.Equal("D41D8CD98F00B204E9800998ECF8427E", ComputeHash(Array.Empty<byte>()));
	}

	[Fact]
	public void produces_correct_hash_for_one_byte_of_data() {
		Assert.Equal("BC9ABF1BE59FFCE180251D4CCE755FE2", ComputeHash(new byte[] { 0x90 }));
	}

	[Fact]
	public void produces_correct_hash_for_large_data() {
		var data = new byte[256 * 1024 * 1024];
		var r = new Random(42);
		r.NextBytes(data);

		Assert.Equal("3A95329838E3E8DC409FAB660938E80E", ComputeHash(data));
	}

	private string ComputeHash(byte[] data) =>
		Convert.ToHexString(_md5!.ComputeHash(data));
}
