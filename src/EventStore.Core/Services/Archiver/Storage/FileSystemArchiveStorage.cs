// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace EventStore.Core.Services.Archiver.Storage;

public class FileSystemArchiveStorage : IArchiveStorage {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FileSystemArchiveStorage>();

	private readonly string _archivePath;

	public FileSystemArchiveStorage(FileSystemOptions options) {
		_archivePath = options.Path;
	}

	public ValueTask StoreChunk(string pathToSourceChunk) {
		var fileName = Path.GetFileName(pathToSourceChunk);
		var pathToDestnationChunk = Path.Combine(_archivePath, fileName);
		Log.Information("Copying file {Path} to {Destination}", pathToSourceChunk, pathToDestnationChunk);
		File.Copy(pathToSourceChunk, pathToDestnationChunk);
		return ValueTask.CompletedTask;
	}
}
