using System;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Diagnostics {




	/*

███████ ████████ ██████  ██    ██  ██████ ████████ ███████ 
██         ██    ██   ██ ██    ██ ██         ██    ██      
███████    ██    ██████  ██    ██ ██         ██    ███████ 
     ██    ██    ██   ██ ██    ██ ██         ██         ██ 
███████    ██    ██   ██  ██████   ██████    ██    ███████ 
                                                           
                                                           
	 */
	public struct ReadStreamForwardsData {
		private readonly Instant _start;

		public ReadStreamForwardsData(Instant start) {
			_start = start;
		}

		public float ElapsedMicroseconds(Instant now) => now.ElapsedMicroseconds(since: _start);
	}

	//qq names/location
	//qq encapsulation
	public struct StatInfo {
		public Instant EnqueuedAt { get; }

		public StatInfo(Instant enqueuedAt) {
			EnqueuedAt = enqueuedAt;
		}
	}













	




















	/*
███████ ███    ██ ██    ██ ███    ███ ███████ 
██      ████   ██ ██    ██ ████  ████ ██      
█████   ██ ██  ██ ██    ██ ██ ████ ██ ███████ 
██      ██  ██ ██ ██    ██ ██  ██  ██      ██ 
███████ ██   ████  ██████  ██      ██ ███████ 
	 */


	//qq
	// 1. All message types need a message number for use with the bus. Requirements
	// - contiguous
	// - does NOT have to be stable.
	//
	// currently we just count them in dynamically at runtime.
	//
	// 2. All messages types that we can to emit stats for need a stats number
	// - stable
	// - does not have to be contiguous
	//
	// But consider, plugins may add more message types that we want to report stats on. they may need
	// stable numbers and we certainly dont want to try to coordinate unique numbers between multiple
	// plugins. instead we give each plugin a namespace and let it deal with its own uniqueness within
	// that space. this does however mean that the numbers will not be contiguous.
	//
	// lets have the core messages use exactly the same mechanisms as plugins to establish the pattern
	// - we should have the persistent subscription messages in their own space too, since they might be 
	//   a plugin later.
	// - we could group the core messages into several spaces.
	//
	// then we just need a way to get the stats id from a message.

	public enum QueueId {
		Main,
		Writer,
		ProjectionMain, //qq
		ProjectionCore, //qq
		Worker1,
		Worker2,
		Reader1, //qq do we want to have each reader queue or just one enum entry for all of them. same for workers
		Reader2,
	}

	//qqqqqqqqqqq hum its all very well having a global registry of groups assigned to plugins, but 
	// what abotu the case where you run an older version of ES with a plugin that was written after
	// that version, so ES doesn't know about the translation. we aren't expecting masse of plugins so it might
	// not be that terrible... but still maybe it would be better not to rely on a compiled in representation
	// of the groups. however it might still be a good idea to have the enum as a source of truth
	//qqqqqq actually it is no longer clear at all that we need to have group ids. we used to have them for array lookups
	//public enum MessageGroup : int {
	//	None = 0,
	//	Dynamic = 1, //qq could be handy for splitting out existing messages by their dynamically assigned numbering
	//				 //qq maybe we want to split core up.. elections.. reads, writes etc.
	//	Core = 2,
	//	Projections = 3,
	//	PersistentSubscriptions = 4,
	//	//qq etc
	//}






























	/*
		██████  ██    ██ ███████ 
		██   ██ ██    ██ ██      
		██████  ██    ██ ███████ 
		██   ██ ██    ██      ██ 
		██████   ██████  ███████ 

		███████  ██████  ██    ██ ██████   ██████ ███████ ███████ 
		██      ██    ██ ██    ██ ██   ██ ██      ██      ██      
		███████ ██    ██ ██    ██ ██████  ██      █████   ███████ 
			 ██ ██    ██ ██    ██ ██   ██ ██      ██           ██ 
		███████  ██████   ██████  ██   ██  ██████ ███████ ███████ 



	We record queue time and processing time for messages passing through the stages.

	todo: We might be able to record envelope time in a generic way too by getting the envelope from a factory that adds timings.
	todo: We want to instrument the queues themselves too - queue length, histogram of times etc.

	(a) We want to be able to collect these stats for messages in different groups (core/projections/plugin1/plugin2/etc)
	(b) We want to be able to configure the different messages to collect,
		- individually: to see what is going on with these messages in particular
		- aggregated together so they're included somewhere but using less resources
		- not at all: because we dont care about this message at all, or it has its own custom processing that renders the generic stats pointless.

	//qq (c) is unnecessary if we can make wiring up each message sufficiently trivial and programatically check that it is done
	(c) We want to be able to, in development time, collect stats against the dynamically generated MessageTypeId
		- but continue to see the messagse that have static ids against those ids.
		- simply to save us having to be able to wire everything before being able to see some results, and to guide where to look next.
		- development time only because the dynamic ids are not static, obviously, so aren't registered in some persistent manifest to make sense of the data.

	Each messagetype is given an EventSource (identifying the group) and a MessageTypeId (enum member) within that source.
	Between them this uniquely identifies the message. Each message type is also given a StatsId which is the enum value that
	the message will report against. This will by default be the same as the MessageTypeId but can be configured to be different.
	The configuration is applied to the message types at startup.
	   setting the statsId to messageTypeId gives us Individual stats
	   setting the statsId to Other gives us Aggregated stats
	   setting the statsId to None  gives us Disabled. (or this might also be an early returning not mapping at all)

	We somehow need to give each message type a unique identity (group/messageTypeId pair) so that we can universally
	interpret the collected stats (which contain the ids not the names). However, because we have plugins, this is easier
	said than done. We can't have a global registry of each message type to coordinate between the core/plugins - this
	would be unmaintainable. Instead we organise them into groups - each group must have a unique name.
	A plugin would use its own groups and be responsible for the numbering 	within those groups (they shouldn't change over
	time though) i dont think they need to be incrementally numbered any more.

	GROUPS

	So each message type has a Group and an Id within that group. By default messages go in the Dynamic group, with an Id of
	the dynamically assigned MsgTypeId. //qqqqq dynamic thing probably not necessary now

	MANIFEST GENERATION

	The manifest that maps the captured numbers to names is generated at compile time according to the method parameters on
	the EventSources. Therefore, a plugin must arrange for this to be done at its own compile time. So it has its own
	EventSource and its own enum per group defined in its source. We try to make the code required in the plugin minimal (
	maybe even code gen?)

	HOOKING INTO THE QUEUES

	The queues track when each item is added and passes this information on to the IHandleEx (ITimedHandle ?)
	A generic IHandleEx implementation decorates the handler that does the actual work, and therefore knows the amount of
	time spent in the queue and the amount of time spent processing. It produces stats for these by getting the
	EventSource _from the message_, saving any lookups.

	The configuration of the mapping that controls aggregation might want to be done per queue later, but I'm not certain
	it would be useful, and would add extra lookups/complexity, so for now we don't have mapping per queue.
	So the event source itself can be responsible for doing the mapping, configured when it is registered.

	REGISTRATION

	We probably dont need much in the way of registration for this because the MessageHierarchy goes and discovers all 
	the messages.






	Cases we want to cover, make sure the dimensions above cover them.
	Todo:
		[ ] See individual stats for core and plugin messages according to their dynamic ids
		[ ] See aggregate stats for messages that dont have a static id
		[ ] Give particular message types static ids so that we can see them individually in prod
		[ ] Disable auto stats for particular messages with static ids (replace with custom because we want more or less info about them)
		[ ] Aggregate auto stats for particular messages with static ids (because we dont want the detailed breakdown but we gave it a number already so it should keep it)
		[ ] Make the above work for plugins. Which just means we need a scheme for numbering messages in plugins uniquely
		[ ] aggregate events differently depending on the queue? if now, how?
				maybe t h


	//qq consider what happens if you do things like provide a message with a static id but not the correct group
	// or fall into other gaps in unexpected combinations of dimensions.


	*/




	public abstract class MyEventSource : EventSource {
		public static readonly string MyName = "EventStore.Core.ExperimentalGenericMeters";
		protected const string TheLogString = "MessageProcessed: queue {0} message {1}. {2:N2}/{3:N2} microseconds";
		protected const int MessageProcessedEventId = 1;
		public abstract void MessageProcessed(
			QueueId queueId,
			int messageTypeId,
			float queueMicroseconds,
			float processingMicroseconds);
	}

	public class MyEventSource<TMessageTypeEnum> : MyEventSource where TMessageTypeEnum : Enum {
		public static MyEventSource<TMessageTypeEnum> Log { get; } = new MyEventSource<TMessageTypeEnum>();
		private static readonly Meter _meter = new(MyName, "0.0.1");

		private static readonly Histogram<float> _queueTime = _meter.CreateHistogram<float>("queue-time", unit: "microseconds");
		private static readonly Histogram<float> _processingTime = _meter.CreateHistogram<float>("processing-time", unit: "microseconds");

		//qq see which of these will be useful in practice. are they effectively free if they aren't being consumed?
		//qq is int big enough
		private static readonly Counter<int> _count = _meter.CreateCounter<int>("count", unit: "messages");
		private static readonly Counter<float> _totalQueueTime = _meter.CreateCounter<float>("total-queue-time", unit: "microseconds");
		private static readonly Counter<float> _totalProcessingTime = _meter.CreateCounter<float>("total-processing-time", unit: "microseconds");

		//qq we can choose how to parameterise the events, perhaps corresponding to the dimensions/tags we put on the metrics
		// especially consider this with respect to
		// - plugins
		// - querying/processing metrics
		//
		// our space for this is queue * messagetype (where the messagetype identifies the group and the message in the group)
		// - queue should definitely be a parameter otherwise if we add another queue we'd have to adjust the plugins.
		// - should message type be a parameter or each have its own event?
		//      - INDIVIDUAL EVENTS
		//           - downside: plugins would have to code their own events and somehow register them to be called
		//      - PARAMETER
		//           - this seems preferable because we could pretty generically handle a lot of cases
		//           - downside: trickiness generating the enum names?
		// - so we try for having both as parameters
		//
		// OK WE COULD HAVE a different eventsource for each plugin, hopefully a minimal amount of work for each plugin to do, just derive
		// from a class and maybe give it a name... and register it but at least it is one class for the plugin rather than each message.

		public override void MessageProcessed(
			QueueId queueId,
			int messageTypeId,
			float queueMicroseconds,
			float processingMicroseconds) {

			//qq make sure calls to WriteEvent do not use the expensive overloads.
			WriteEvent(1, (int)queueId, messageTypeId, queueMicroseconds, processingMicroseconds);

			//qqq if, in the end, we figure that the tags aren't good for performance then we instead would have a set of meters
			// per queue*message type, and i guess look them up in arrays
			//qqqq hmm does this mean boxing the objects, if so maybe store the boxed objects in an array and reuse them?
			//qqqq how on earth does it know in the meter that queueId is a QueueId and therefore what label to give it
			//  is that part of the manifest somehow, or is is sneakily storing the string?

			//qqqq i think we are pretty much going to need separate histograms or maybe meters per messages group
			// unless we also tag the output with the groups

			//qq we dont need the type parameter at all, we could just pass the tags in cause they're objects here
			var e = Unsafe.As<int, TMessageTypeEnum>(ref messageTypeId);
			var queueTag = new System.Collections.Generic.KeyValuePair<string, object>("Queue", queueId);
			var messageTypeTag = new System.Collections.Generic.KeyValuePair<string, object>("MessageType", e);


			//qqqq chat
			// instead of storing the statsid on the message, we could store the eventsource itself,
			// by using a builder to instantiate the message
			// the builder can also decorate the envelope with the appropriate metricy stuff


			// _count.Enabled //qq useful to check this?
			//qqqqqqq attempting this with multidimensional metrics to see if they are any good to us.
			//qqqq its probably not very useful to have a metric for queue time per message
			_queueTime.Record(queueMicroseconds, queueTag/*, messageTypeTag*/);
			_processingTime.Record(processingMicroseconds, queueTag, messageTypeTag);
			_count.Add(1, queueTag, messageTypeTag);
			_totalQueueTime.Add(queueMicroseconds, queueTag, messageTypeTag);
			_totalProcessingTime.Add(processingMicroseconds, queueTag, messageTypeTag);
		}

		//qq remember to put fixed size payload data first
		//qq event types can be given keywords... we might want Reads/Writes/Elections etc. so that they can be enabled in groups
		//      they also have Tasks and OpCodes (what is the difference to keywords?) start/stop creates tasks implicity
		//qq make sure not to call the params overload of writeevent because of the additional overhead.

		//qq the default activityids are not suitable for us because they follow the async context... but perf view and other tooling uses them to build a tree
		// of messages. i wonder if we can set the activity ids manually, or if tools like perfview can construct a tree based on other fields
		//   yes we can control the activityids with SetCurrentThreadActivityId and WriteEventWithRelatedActivityId (for the first event of the activity)
	}

















































	/*
	 █████  ██    ██ ████████  ██████  
	██   ██ ██    ██    ██    ██    ██ 
	███████ ██    ██    ██    ██    ██ 
	██   ██ ██    ██    ██    ██    ██ 
	██   ██  ██████     ██     ██████  


	*/

	//qq name
	public static class StatsFactory {
		public static bool StatsEnabled { get; set; } = true; //qq default?
		public static IHandleEx<T> Create<T>(QueueId queueId, IHandleEx<T> wrapped) where T : Message =>
			StatsEnabled
				? QueueStatsPerMessage.Create(queueId, wrapped)
				: wrapped;
	}

	public class QueueStatsPerMessage {
		public static QueueStatsPerMessage<T> Create<T>(QueueId queueId, IHandleEx<T> wrapped) where T : Message =>
			new(queueId, wrapped);
	}

	//qq name/location
	//qq this thing adds generic stats logging and metrics for an IHandleEx
	public class QueueStatsPerMessage<T> : IHandleEx<T> where T : Message {
		private readonly QueueId _queueId;
		readonly IHandleEx<T> _wrapped;

		public QueueStatsPerMessage(QueueId queueId, IHandleEx<T> wrapped) {
			_queueId = queueId;
			_wrapped = wrapped;
		}

		public void Handle(StatInfo info, T message) {
			//qq this is coupled with the fact that we are source-generating these members
			// for the base message class. how should the dependencies go here
			var messageTypeId = message.StatsId;
			var eventSource = message.EventSource;

			var startedProcessingAt = Time.CurrentInstant;
			_wrapped.Handle(info, message);

			if (messageTypeId == 0) {
				return;
			}

			var queueMicroseconds = startedProcessingAt.ElapsedMicroseconds(since: info.EnqueuedAt);
			var processingMicroseconds = Time.CurrentInstant.ElapsedMicroseconds(since: startedProcessingAt);

			//qq perhaps we want to pass some kind of queue identifier as a parameter in the constructor,
			//or maybe the queue identifier... which is the stageidentifier effectively...
			//    should go in the StatInfo.
			//qq both the queue and the message numbers will be small, we could use arrays to find the metric
			//we want if they aren't just dimensions of a single metric

			eventSource.MessageProcessed(
				queueId: _queueId,
				messageTypeId: messageTypeId,
				queueMicroseconds: queueMicroseconds,
				processingMicroseconds: processingMicroseconds);
		}
	}

	//qq name/location
	public class HandleExtender {
		public static HandleExtender<T> Create<T>(IHandle<T> wrapped) where T : Message => new(wrapped);
	}

	//qq name/location
	// this just plugs a IHandle into a place that needs a IHandleEx. hopefully temporary.
	public class HandleExtender<T> : IHandleEx<T> where T : Message {
		readonly IHandle<T> _wrapped;

		public HandleExtender(IHandle<T> wrapped) {
			_wrapped = wrapped;
		}

		public void Handle(StatInfo info, T message) {
			_wrapped.Handle(message);
		}
	}
}
