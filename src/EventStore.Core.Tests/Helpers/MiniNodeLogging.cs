using System;
using Serilog;
using Serilog.Sinks.InMemory;

namespace EventStore.Core.Tests.Helpers; 

public static class MiniNodeLogging {
	public static void Setup() {
		Serilog.Log.Logger = new LoggerConfiguration()
			.WriteTo.InMemory()
			.CreateLogger();
	}
	
	public static void WriteLogs() {
		foreach (var e in InMemorySink.Instance.LogEvents) {
			Console.Error.WriteLine($"MiniNode: {e.RenderMessage()}");
			if (e.Exception != null) {
				Console.Error.WriteLine($"MiniNode: {e.Exception}");
			}
		}
	}

	public static void Clear() {
		InMemorySink.Instance.Dispose();
	}
}
