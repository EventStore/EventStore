// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Auth.StreamPolicyPlugin;

public class AccessPolicy {
	public readonly string[] Readers;
	public readonly string[] Writers;
	public readonly string[] Deleters;
	public readonly string[] MetadataReaders;
	public readonly string[] MetadataWriters;

	private AccessPolicy() {
		Readers = [];
		Writers = [];
		Deleters = [];
		MetadataReaders = [];
		MetadataWriters = [];
	}

	public AccessPolicy(
		string[] readers, string[] writers, string[] deleters, string[] metadataReaders, string[] metadataWriters) {
		Readers = readers ?? [];
		Writers = writers ?? [];
		Deleters = deleters ?? [];
		MetadataReaders = metadataReaders ?? [];
		MetadataWriters = metadataWriters ?? [];
	}
	public static AccessPolicy None => new();

	public override string ToString() {
		return $"$r: {string.Join(',', Readers)}\n" +
		       $"$w: {string.Join(',', Writers)}\n" +
		       $"$d: {string.Join(',', Deleters)}\n" +
		       $"$mr: {string.Join(',', MetadataReaders)}\n" +
		       $"$mw: {string.Join(',', MetadataWriters)}\n";
	}
}
