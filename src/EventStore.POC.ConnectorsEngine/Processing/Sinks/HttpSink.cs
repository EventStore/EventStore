// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.IO.Core;
using Polly;
using Polly.Retry;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing.Sinks;

public partial class HttpSink : ISink {
	private const int MaxRetry = int.MaxValue;

	private readonly string _connectorId;
	private readonly ILogger _logger;
	private readonly HttpClient _client;
	private readonly MediaTypeHeaderValue _jsonContentTypeHeader;
	private readonly AsyncRetryPolicy _retryPolicy;

	//qq dispose
	public HttpSink(string connectorId, Uri config, ILogger logger) {
		_connectorId = connectorId;
		Config = config;
		_logger = logger;
		_client = new HttpClient { BaseAddress = UrlParser.GetBaseAddress(config) };
		_jsonContentTypeHeader = new MediaTypeHeaderValue("application/json");

		_retryPolicy = Policy
			.Handle<HttpRequestException>(ex => {
				if (ex.HttpRequestError
				    is HttpRequestError.ConnectionError
				    or HttpRequestError.NameResolutionError) {
					return true;
				}

				return ex.StatusCode is >= HttpStatusCode.BadRequest;
			})
			.WaitAndRetryAsync(MaxRetry, RetryDelay, OnRetry);

		TimeSpan RetryDelay(int retryCount) =>
			TimeSpan.FromSeconds(
				Math.Clamp(
					value: retryCount * retryCount * 0.2,
					min: 1,
					max: 10));

		void OnRetry(Exception _, TimeSpan __, int retry, Context ___) {
			_logger.Warning(
				"HttpSink: Connector {connector} post to {target} failed, retrying ({retry}/{maxRetry})",
				_connectorId, _client.BaseAddress, retry, MaxRetry);
		}
	}

	public Uri Config { get; }

	public async Task Write(Event evt, CancellationToken ct) {
		_logger.Verbose("HttpSink: Connector {connector} received an event: {stream} : {event}",
			_connectorId, evt.Stream, evt.EventType);

		if (evt.ContentType != "application/json") {
			_logger.Verbose("HttpSink: skipped binary event for now"); //qq
			// new ByteArrayContent(evt.Data.ToArray()), _binaryContentTypeHeader)
			return;
		}

		using var content = new ReadOnlyMemoryContent(evt.Data);

		content.Headers.ContentType = _jsonContentTypeHeader;
		content.Headers.Add("es-event-id", $"{evt.EventId:N}");
		content.Headers.Add("es-created", $"{evt.Created}");
		content.Headers.Add("es-stream", evt.Stream);
		content.Headers.Add("es-event-number", $"{evt.EventNumber}");
		content.Headers.Add("es-event-type", evt.EventType);
		content.Headers.Add("es-content-type", evt.ContentType);
		content.Headers.Add("es-commit-position", $"{evt.CommitPosition}");
		content.Headers.Add("es-prepare-position", $"{evt.PreparePosition}");
		content.Headers.Add("es-is-redacted", $"{evt.IsRedacted}");

		//qq decide what to do about metadata really
		if (evt.Metadata.Span.TryParseUtf8(out var parsed)) {
			if (parsed is JsonObject job) {
				foreach (var kvp in job) {
					//qq considering casing
					//qq consider nested metadata
					content.Headers.Add($"es-metadata-{kvp.Key}", $"{kvp.Value}");
				}
			}
		}

		var path = UrlParser.GetPath(Config, evt);

		//qq todo: avoid allocating the lambda on every Write
		await _retryPolicy.ExecuteAsync(async () => {
			using var resp = await _client.PostAsync(path, content, ct);

			var status = resp.StatusCode;
			// var respContent = await resp.Content.ReadAsStringAsync(ct);

			var ok = status is >= (HttpStatusCode)200 and < (HttpStatusCode)300;
			if (!ok)
				throw new HttpRequestException($"Error calling HTTP endpoint {path}: {status}", inner: null, status);

			_logger.Verbose(
				"HttpSink: Connector {connector} successfully posted content to: {url}, StatusCode:{status}",
				_connectorId, _client.BaseAddress, status);
		});
	}

	public static partial class UrlParser {
		public static Uri GetBaseAddress(Uri fullUri) => new($"{fullUri.Scheme}://{fullUri.Host}:{fullUri.Port}");

		public static string GetPath(Uri fullUri, Event evt) =>
			_extractTokensPattern.Replace(
				fullUri.LocalPath,
				m => _getUrlParts.TryGetValue(m.Groups[1].Value, out var v) ? v(evt) : m.Value);

		private static readonly Dictionary<string, Func<Event, string>> _getUrlParts = new() {
			["eventType"] = e => e.EventType,
			["stream"] = e => e.Stream,
			["category"] = e => {
				var firstDelimiterIndex = e.Stream.IndexOf('-');
				return firstDelimiterIndex == -1
					? e.Stream
					: e.Stream[..firstDelimiterIndex];
			}
		};

		private static readonly Regex _extractTokensPattern = ExtractTokensRegex();

		[GeneratedRegex(@"\{(.+?)\}", RegexOptions.Compiled)]
		private static partial Regex ExtractTokensRegex();
	}
}
