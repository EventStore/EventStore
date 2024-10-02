// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using ILogger = Serilog.ILogger;

namespace EventStore.Transport.Http {
	public class IOStreams {
		private static readonly ILogger Log = Serilog.Log.ForContext<IOStreams>();

		public static void SafelyDispose(params Stream[] streams) {
			if (streams == null || streams.Length == 0)
				return;

			foreach (var stream in streams) {
				try {
					if (stream != null)
						stream.Dispose();
				} catch (Exception e) {
					//Exceptions may be thrown when client shutdowned and we were unable to write all the data,
					//Nothing we can do, ignore (another option - globally ignore write errors)
					Log.Information("Error while closing stream : {e}", e.Message);
				}
			}
		}
	}
}
