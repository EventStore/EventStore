// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Grpc.Core;
using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace EventStore.OtlpExporterPlugin.Tests;

public class FakeCollector(TaskCompletionSource<ExportMetricsServiceRequest> tcs) : MetricsService.MetricsServiceBase {
	public override Task<ExportMetricsServiceResponse> Export(ExportMetricsServiceRequest request, ServerCallContext context) {
		tcs.TrySetResult(request);
		return Task.FromResult(new ExportMetricsServiceResponse());
	}
}
