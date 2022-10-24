using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using EventStore.Core.Diagnostics;


//qq rename
//qq location
[EventSource(Name = "eventstore-experiments-grpc")]
class MyEventSourceGrpc : EventSource {
	static readonly Meter _meter = new("EventStore.Core.ExperimentalGrpcMeters", "0.0.1");

	private static Histogram<float> _readStreamForwardElapsed = _meter.CreateHistogram<float>("read-stream-elapsed", unit: "microseconds");

	public static MyEventSourceGrpc Log { get; } = new MyEventSourceGrpc();

	//qq being a start/stop pair, these come with an implicit task and two opcodes (i think ??)
	[Event(1, Version = 0, Level = EventLevel.Informational, Message = "ReadStreamForwardsStart {0}")]
	public void ReadStreamForwardsStart(string streamName) {
		WriteEvent(1, streamName);
	}

	//qq make sure this is appropriately correlated with the start (avtivity ids?). make sure the correlation is tested too
	//qqqq if we do it with float maybe we should just pass in elapsedSeconds
	[Event(2, Version = 0, Level = EventLevel.Informational, Message = "ReadStreamForwardsStop. Took {0} microseconds")]
	public void ReadStreamForwardsStop(float elapsedMicroseconds) {
		WriteEvent(2, elapsedMicroseconds);

		//qqq although, maybe we want to be able to turn off the Events without turning off the metric?
		//qq presumably if we turn off the event we still get the metric, and vice versa?
		_readStreamForwardElapsed.Record(elapsedMicroseconds);
	}

	//qqqqq https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource-instrumentation#supported-parameter-types
	// ^ this claims we can pass TimeSpans or even our own EventData structs to Write (applies to net core 3.1+ and net 4.5+)
	// but it didn't seem to work when i tried it. adding these helpers for now to convert to more basic types.
	//qq consder whether this is the right place for it
	public static class ReadStreamForwards {
		public static ReadStreamForwardsData Start(string streamName) {
			Log.ReadStreamForwardsStart(streamName);
			return new(Time.CurrentInstant);
		}

		public static void Stop(ReadStreamForwardsData data) {
			var now = Time.CurrentInstant;
			Log.ReadStreamForwardsStop(
				elapsedMicroseconds: data.ElapsedMicroseconds(now));
		}
	}
}
