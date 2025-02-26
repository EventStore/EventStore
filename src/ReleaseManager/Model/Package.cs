// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace ReleaseManager.Model;

public record Package {
	public string filename { get; init; } = "";
	public string format { get; init; } = "";
	public string identifier_perm { get; init; } = "";
	public PackageTags tags { get; init; } = new();
	public string repository { get; init; } = "";
	public string version { get; init; } = "";

	public string Pretty => $"{filename} {format} {tags.version.FirstOrDefault()}";

	public record PackageTags {
		public string[] version { get; init; } = [];
	}
}
