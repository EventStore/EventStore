// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Cryptography;
using EventStore.Plugins;
using EventStore.Plugins.Diagnostics;
using EventStore.Plugins.Licensing;
using EventStore.Plugins.Tests;
using EventStore.Plugins.Transforms;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Security.EncryptionAtRest.Tests;

public class EncryptionAtTestPluginTests {
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void can_collect_telemetry(bool enabled) {
		using var sut = new EncryptionAtRestPlugin();
		using var collector = PluginDiagnosticsDataCollector.Start(sut.DiagnosticsName);

		IConfigurationBuilder configBuilder = new ConfigurationBuilder();

		if (enabled)
			configBuilder = configBuilder.AddInMemoryCollection(new Dictionary<string, string?> {
				{"EventStore:EncryptionAtRest:Enabled", "true"},
				{"EventStore:EncryptionAtRest:MasterKey:File:KeyPath", "./keys"},
				{"EventStore:EncryptionAtRest:Encryption:AesGcm:Enabled", "true"},
			});

		var config = configBuilder.Build();

		var builder = WebApplication.CreateBuilder();
		builder.Services
			.AddSingleton<ILicenseService>(new Fixtures.FakeLicenseService())
			.AddSingleton<IReadOnlyList<IDbTransform>>([]);

		// when
		((IPlugableComponent)sut).ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();
		((IPlugableComponent)sut).ConfigureApplication(app, config);

		// then
		collector.CollectedEvents(sut.DiagnosticsName).Should().ContainSingle().Which
			.Data["enabled"].Should().Be(enabled);
	}

	[Theory]
	[InlineData(true, true, "ENCRYPTION_AT_REST", false)]
	[InlineData(true, false, "ENCRYPTION_AT_REST", true)]
	[InlineData(false, true, "ENCRYPTION_AT_REST", false)]
	[InlineData(false, false, "ENCRYPTION_AT_REST", false)]
	[InlineData(true, true, "NONE", true)]
	[InlineData(true, false, "NONE", true)]
	[InlineData(false, true, "NONE", false)]
	[InlineData(false, false, "NONE", false)]
	public void respects_license(bool enabled, bool licensePresent, string entitlement, bool expectedException) {
		// given
		using var sut = new EncryptionAtRestPlugin();

		IConfigurationBuilder configBuilder = new ConfigurationBuilder();

		if (enabled)
			configBuilder = configBuilder.AddInMemoryCollection(new Dictionary<string, string?> {
				{"EventStore:EncryptionAtRest:Enabled", "true"},
				{"EventStore:EncryptionAtRest:MasterKey:File:KeyPath", "./keys"},
				{"EventStore:EncryptionAtRest:Encryption:AesGcm:Enabled", "true"},
			});

		var config = configBuilder.Build();

		var builder = WebApplication.CreateBuilder();
		builder.Services.AddSingleton<IReadOnlyList<IDbTransform>>([]);

		var licenseService = new Fixtures.FakeLicenseService(licensePresent, entitlement);
		builder.Services.AddSingleton<ILicenseService>(licenseService);

		((IPlugableComponent)sut).ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();

		// when
		((IPlugableComponent)sut).ConfigureApplication(app, config);

		// then
		if (expectedException) {
			Assert.NotNull(licenseService.RejectionException);
		} else {
			Assert.Null(licenseService.RejectionException);
		}
	}

	[Fact]
	public void works() {
		// given
		using var sut = new EncryptionAtRestPlugin();

		IConfigurationBuilder configBuilder = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> {
				{"EventStore:EncryptionAtRest:Enabled", "true"},
				{"EventStore:EncryptionAtRest:MasterKey:File:KeyPath", "./keys"},
				{"EventStore:EncryptionAtRest:Encryption:AesGcm:Enabled", "true"},
			});

		var config = configBuilder.Build();

		// when
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddSingleton<ILicenseService>(new Fixtures.FakeLicenseService());
		builder.Services.AddSingleton<IReadOnlyList<IDbTransform>>([]);

		((IPlugableComponent)sut).ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();
		((IPlugableComponent)sut).ConfigureApplication(app, config);

		// then
		var transforms = app.Services.GetService<IReadOnlyList<IDbTransform>>();

		Assert.NotNull(transforms);
		Assert.Single(transforms);
		Assert.Equal(TransformType.Encryption_AesGcm, transforms[0].Type);

		var chunk = Path.GetTempFileName();
		try {
			VerifyTransformWorks(transforms[0], chunk);
		} finally {
			File.Delete(chunk);
		}
	}

	private static void VerifyTransformWorks(IDbTransform dbTransform, string chunk) {
		const int dataSize = 1_000_000;
		const int chunkHeaderSize = 128; // this value is hardcoded in the transform and cannot be changed in the test
		const int footerSize = 128;
		const int alignmentSize = 4096;

		var writeHeader = new byte[chunkHeaderSize];
		var writeData = new byte[dataSize];
		var writeFooter = new byte[footerSize];

		RandomNumberGenerator.Fill(writeHeader);
		RandomNumberGenerator.Fill(writeData);
		RandomNumberGenerator.Fill(writeFooter);

		WriteFirstPart(writeHeader, writeData[..(dataSize/3)]);
		WriteMiddlePart(dataSize / 3, writeData[(dataSize/3)..(dataSize*2/3)]);
		WriteLastPart(dataSize * 2 / 3, writeData[(dataSize*2/3)..], writeFooter, out var writeHash);

		VerifyReads();
		VerifyRandomReads(numRandomReads: 1000);

		return;

		void WriteFirstPart(ReadOnlySpan<byte> header, ReadOnlySpan<byte> data) {
			var transformHeader = dbTransform.ChunkFactory.CreateTransformHeader();
			var transform = dbTransform.ChunkFactory.CreateTransform(transformHeader);

			using var fileStream = File.Open(chunk, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			fileStream.Write(header);
			fileStream.Write(transformHeader.Span);

			using var checksum = MD5.Create();
			checksum.TransformBlock(header.ToArray(), 0, header.Length, null, 0);
			checksum.TransformBlock(transformHeader.ToArray(), 0, transformHeader.Length, null, 0);

			using var writeStream = transform.Write.TransformData(new ChunkDataWriteStream(fileStream, checksum));
			WriteData(writeStream, data);
		}

		void WriteMiddlePart(long writeResumePosition, ReadOnlySpan<byte> data) {
			using var fileStream = File.Open(chunk, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			var transform = CreateTransform(fileStream, out _);

			using var checksum = MD5.Create();
			fileStream.Position = 0;
			using var writeStream = transform.Write.TransformData(new ChunkDataWriteStream(fileStream, checksum));
			writeStream.Position = chunkHeaderSize + writeResumePosition;
			WriteData(writeStream, data);
		}

		void WriteLastPart(long writeResumePosition, ReadOnlySpan<byte> data, ReadOnlySpan<byte> footer, out byte[] hash) {
			using var fileStream = File.Open(chunk, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			var transform = CreateTransform(fileStream, out _);

			using var checksum = MD5.Create();
			fileStream.Position = 0;
			using var writeStream = transform.Write.TransformData(new ChunkDataWriteStream(fileStream, checksum));
			writeStream.Position = chunkHeaderSize + writeResumePosition;
			WriteData(writeStream, data);

			transform.Write.CompleteData(footerSize, alignmentSize);
			transform.Write.WriteFooter(footer, out _);
			checksum.TransformFinalBlock(footer.ToArray(), 0, footer.Length);
			hash = checksum.Hash!;
		}

		void WriteData(Stream stream, ReadOnlySpan<byte> data) {
			while (data.Length > 0) {
				var len = Random.Shared.Next(data.Length + 1);
				stream.Write(data[..len]);
				stream.Flush();
				data = data[len..];
			}
			stream.Flush();
		}

		void VerifyReads() {
			using var fileStream = File.Open(chunk, FileMode.Open, FileAccess.Read, FileShare.None);
			var fileData = new byte[fileStream.Length];
			fileStream.ReadExactly(fileData);
			var stream = new MemoryStream(fileData);
			var transform = CreateTransform(stream, out var header);

			var data = new byte[dataSize];
			using (var readStream = transform.Read.TransformData(new ChunkDataReadStream(stream))) {
				readStream.Position = chunkHeaderSize;
				readStream.ReadExactly(data);
			}

			var footer = new byte[footerSize];
			fileData[^footer.Length..].AsSpan().CopyTo(footer);

			using var checksum = MD5.Create();
			checksum.ComputeHash(fileData);

			Assert.True(new FileInfo(chunk).Length % alignmentSize == 0, "file size is not aligned");
			Assert.True(header.SequenceEqual(writeHeader), "header does not match");
			Assert.True(data.SequenceEqual(writeData), "data does not match");
			Assert.True(footer.SequenceEqual(writeFooter), "footer does not match");
			Assert.True(checksum.Hash!.SequenceEqual(writeHash), "checksum does not match");
		}

		void VerifyRandomReads(int numRandomReads) {
			using var fileStream = File.Open(chunk, FileMode.Open, FileAccess.Read, FileShare.None);
			var fileData = new byte[fileStream.Length];
			fileStream.ReadExactly(fileData);
			var stream = new MemoryStream(fileData);
			var transform = CreateTransform(stream, out _);

			using (var readStream = transform.Read.TransformData(new ChunkDataReadStream(stream))) {
				for (var i = 0; i < numRandomReads; i++) {
					var startPos = Random.Shared.Next(dataSize);
					var endPos = Math.Min(dataSize, startPos + Random.Shared.Next(10000));

					var len = endPos - startPos;
					var buffer = new byte[len];

					readStream.Position = chunkHeaderSize + startPos;

					// split the reads to cover more scenarios
					var span = buffer.AsSpan();
					while (span.Length > 0) {
						var slice = span[..Random.Shared.Next(1, span.Length + 1)];
						span = span[slice.Length..];
						readStream.ReadExactly(slice);
					}

					Assert.True(buffer.SequenceEqual(writeData[startPos..endPos]), "random data read does not match");
				}
			}
		}

		IChunkTransform CreateTransform(Stream stream, out byte[] chunkHeader) {
			chunkHeader = new byte[chunkHeaderSize];
			stream.ReadExactly(chunkHeader);
			var transformHeader = dbTransform.ChunkFactory.ReadTransformHeader(stream);
			return dbTransform.ChunkFactory.CreateTransform(transformHeader);
		}
	}

}
