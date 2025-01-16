// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;

namespace EventStore.Core.Services.Archive.Storage;

public class FileSystemBlobStorage : IBlobStorage {
	private static readonly SearchValues<char> InvalidFileNameChars = SearchValues.Create(Path.GetInvalidFileNameChars());

	private readonly string _archivePath;
	private readonly FileStreamOptions _fileStreamOptions;

	public FileSystemBlobStorage(FileSystemOptions options) {
		_archivePath = options.Path;
		_fileStreamOptions = new FileStreamOptions {
			Access = FileAccess.Read,
			Mode = FileMode.Open,
			Options = FileOptions.Asynchronous,
		};
	}

	public async ValueTask<int> ReadAsync(string name, Memory<byte> buffer, long offset, CancellationToken ct) {
		var targetPath = Path.Combine(_archivePath, name);
		using var handle = File.OpenHandle(targetPath, _fileStreamOptions.Mode, _fileStreamOptions.Access,
			_fileStreamOptions.Share,
			_fileStreamOptions.Options);
		return await RandomAccess.ReadAsync(handle, buffer, offset, ct);
	}

	public ValueTask<BlobMetadata> GetMetadataAsync(string name, CancellationToken token) {
		ValueTask<BlobMetadata> task;
		if (token.IsCancellationRequested) {
			task = ValueTask.FromCanceled<BlobMetadata>(token);
		} else {
			try {
				var targetPath = Path.Combine(_archivePath, name);
				task = ValueTask.FromResult<BlobMetadata>(new(Size: new FileInfo(targetPath).Length));
			} catch (Exception e) {
				task = ValueTask.FromException<BlobMetadata>(e);
			}
		}

		return task;
	}

	public async ValueTask StoreAsync(Stream sourceData, string name, CancellationToken ct) {
		if (MemoryExtensions.IndexOfAny(name, InvalidFileNameChars) >= 0)
			throw new ArgumentOutOfRangeException(nameof(name));

		var destinationPath = Path.Combine(_archivePath, name);
		var tempPath = $"{destinationPath}.tmp";
		if (File.Exists(tempPath))
			File.Delete(tempPath);

		var handle = File.OpenHandle(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			FileOptions.Asynchronous, preallocationSize: sourceData.CanSeek ? sourceData.Length : 0L);
		var outputStream = handle.AsUnbufferedStream(FileAccess.Write);
		try {
			await sourceData.CopyToAsync(outputStream, ct);
			await outputStream.FlushAsync(ct);
		} catch when (File.Exists(tempPath)) {
			File.Delete(tempPath);
			throw;
		} finally {
			await outputStream.DisposeAsync();
			handle.Dispose();
		}

		File.Move(tempPath, destinationPath, overwrite: true);
	}
}
