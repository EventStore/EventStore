// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;

namespace EventStore.Core.Services.Archive.Storage;

public class FileSystemBlobStorage : IBlobStorage {
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

	public async ValueTask Store(Stream sourceData, string name, CancellationToken ct) {
		var destinationPath = Path.Combine(_archivePath, name);
		using var handle = File.OpenHandle(destinationPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None,
			FileOptions.Asynchronous);

		await using var output = handle.AsUnbufferedStream(FileAccess.Write);
		await sourceData.CopyToAsync(output, ct);
		await output.FlushAsync(ct);
	}

	public async ValueTask Store(string input, string name, CancellationToken ct) {
		var destinationPath = Path.Combine(_archivePath, name);
		var tempPath = $"{destinationPath}.tmp";

		if (File.Exists(tempPath))
			File.Delete(tempPath);

		{
			await using var source = File.Open(
				path: input,
				options: new FileStreamOptions {
					Mode = FileMode.Open,
					Access = FileAccess.Read,
					Share = FileShare.Read,
					Options = FileOptions.SequentialScan | FileOptions.Asynchronous
				});

			await using var destination = File.Open(
				path: tempPath,
				options: new FileStreamOptions {
					Mode = FileMode.CreateNew,
					Access = FileAccess.ReadWrite,
					Share = FileShare.None,
					Options = FileOptions.Asynchronous,
					PreallocationSize = new FileInfo(input).Length
				});

			await source.CopyToAsync(destination, ct);
		}

		File.Move(tempPath, destinationPath, overwrite: true);
	}
}
