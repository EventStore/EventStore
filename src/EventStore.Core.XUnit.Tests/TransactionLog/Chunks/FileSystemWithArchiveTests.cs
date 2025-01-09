// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Chunks;

public class FileSystemWithArchiveTests {
	private readonly FakeFileSystem _local;
	private readonly FileSystemWithArchive _sut;
	private readonly string _localLocator;
	private readonly string _remoteLocator;

	public FileSystemWithArchiveTests() {
		_local = new FakeFileSystem();
		var codec = new PrefixingLocatorCodec();
		_sut = new FileSystemWithArchive(
			chunkSize: 256 * 1024 * 1024,
			locatorCodec: codec,
			localFileSystem: _local,
			remoteFileSystem: new ArchiveBlobFileSystem(),
			archive: NoArchiveReader.Instance);
		var chunk = "chunk-000.000";
		_localLocator = codec.EncodeLocalName(chunk);
		_remoteLocator = codec.EncodeRemoteName(chunk);

	}

	[Fact]
	public void can_open_local() {
		var _ = _sut.OpenForReadAsync(_localLocator, default, default);
		Assert.Equal(1, _local.OpenCount);
	}

	// todo
	//[Fact]
	//public void can_open_remote() {
	//	var _ = _sut.OpenForReadAsync(_remoteLocator, default, default);
	//	Assert.Equal(0, _local.OpenCount);
	//}

	[Fact]
	public void can_read_footer_local() {
		var _ = _sut.ReadFooterAsync(_localLocator, default);
		Assert.Equal(1, _local.FooterCount);
	}

	[Fact]
	public async Task cannot_read_footer_remote() {
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await _sut.ReadFooterAsync(_remoteLocator, default));
		Assert.Equal(0, _local.FooterCount);
	}

	[Fact]
	public void can_read_header_local() {
		var _ = _sut.ReadHeaderAsync(_localLocator, default);
		Assert.Equal(1, _local.HeaderCount);
	}

	[Fact]
	public async Task cannot_read_header_remote() {
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await _sut.ReadHeaderAsync(_remoteLocator, default));
		Assert.Equal(0, _local.HeaderCount);
	}

	// GetChunks is covered in test class `with_tfchunk_enumerator`

	class FakeFileSystem : IChunkFileSystem {
		public int OpenCount { get; private set; }
		public int HeaderCount { get; private set; }
		public int FooterCount { get; private set; }

		public IVersionedFileNamingStrategy NamingStrategy => throw new NotImplementedException();

		public IChunkFileSystem.IChunkEnumerable GetChunks() {
			throw new NotImplementedException();
		}

		public ValueTask<IChunkHandle> OpenForReadAsync(string fileName, IBlobFileSystem.ReadOptimizationHint hint, CancellationToken token) {
			OpenCount++;
			return ValueTask.FromResult<IChunkHandle>(null);
		}

		public ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token) {
			FooterCount++;
			return ValueTask.FromResult<ChunkFooter>(null);
		}

		public ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token) {
			HeaderCount++;
			return ValueTask.FromResult<ChunkHeader>(null);
		}
	}
}
