// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Settings {

	public static class ThreadCountCalculator {
		private const int ReaderThreadCountFloor = 4;

		public static int CalculateReaderThreadCount(int configuredCount, int processorCount,
			bool isRunningInContainer) {
			if (configuredCount > 0) {
				Log.Information(
					"ReaderThreadsCount set to {readerThreadsCount:N0}. " +
					"Calculated based on processor count of {processorCount:N0} and configured value of {configuredCount:N0}",
					configuredCount,
					processorCount, configuredCount);
				return configuredCount;
			}

			if (isRunningInContainer) {
				Log.Information(
					"ReaderThreadsCount set to {readerThreadsCount:N0}. " +
					"Calculated based on containerized environment and configured value of {configuredCount:N0}",
					ContainerizedEnvironment.ReaderThreadCount,
					configuredCount);
				return ContainerizedEnvironment.ReaderThreadCount;
			}

			var readerCount = Math.Clamp(processorCount * 2, ReaderThreadCountFloor, 16);
			Log.Information(
				"ReaderThreadsCount set to {readerThreadsCount:N0}. " +
				"Calculated based on processor count of {processorCount:N0} and configured value of {configuredCount:N0}",
				readerCount,
				processorCount, configuredCount);
			return readerCount;
		}

		public static int CalculateWorkerThreadCount(int configuredCount, int readerCount, bool isRunningInContainer) {
			if (configuredCount > 0) {
				Log.Information(
					"WorkerThreads set to {workerThreadsCount:N0}. " +
					"Calculated based on a reader thread count of {readerThreadsCount:N0} and a configured value of {configuredCount:N0}",
					configuredCount,
					readerCount, configuredCount);
				return configuredCount;
			}


			if (isRunningInContainer) {
				Log.Information(
					"WorkerThreads set to {workerThreadsCount:N0}. " +
					"Calculated based on containerized environment and a configured value of {configuredCount:N0}",
					ContainerizedEnvironment.WorkerThreadCount,
					configuredCount);
				return ContainerizedEnvironment.WorkerThreadCount;
			}

			var workerThreadsCount = readerCount > ReaderThreadCountFloor ? 10 : 5;
			Log.Information(
				"WorkerThreads set to {workerThreadsCount:N0}. " +
				"Calculated based on a reader thread count of {readerThreadsCount:N0} and a configured value of {configuredCount:N0}",
				workerThreadsCount,
				readerCount, configuredCount);
			return workerThreadsCount;
		}
	}
}
