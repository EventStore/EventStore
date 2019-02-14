using System;
using System.IO;
using EventStore.Common.Log;

namespace EventStore.Transport.Http {
	public class IOStreams {
		private static readonly ILogger Log = LogManager.GetLoggerFor<IOStreams>();

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
					Log.Info("Error while closing stream : {e}", e.Message);
				}
			}
		}
	}
}
