//using System.Diagnostics.Tracing;

////qq I wonder if we need the dynamic source now... if we make it easy to add the functionality to
//// all the messages, and we do a runtime or compiletime check that they are all wired up approprately
//// then we needn't have dynamic numbering at all.
////
//// what other features of dynamic handling did we have
//// - disabling aggregated message - thats fine we wont have any
//// - aggregating dynamic messages, thats fine we wont have any
//// - disabling/aggregating it (thats fine we can provide a layer of configuration that disables things
////   by default if the user hasn't enabled them
//namespace EventStore.Core.Diagnostics {
//	//qqqq rename/locate, this is no longer 'dynamic' but some kind of default
//	public enum DynamicMessage : int {
//		None = 0,
//		Other = int.MaxValue,
//	}

//	//qqq this isn't right because for dynamic it needs to get the 
//	[EventSource(Name = "eventstore-experiments-dynamic")]
//	class DynamicMessageSource : MyEventSource<DynamicMessage> {
//		public static DynamicMessageSource LogJunk { get; } = new DynamicMessageSource();

//		//qqqq what if we want to change the version later, does that make any sense for the plugins?
//		//   say if we added an argument, i guess we would have a different helper overload for that and the plugin would adjust the version when
//		//   it  started calling it
//		[Event(MessageProcessedEventId, Version = 0, Level = EventLevel.Informational, Message = "Dynamic " + TheLogString)]
//		public void MessageProcessed(QueueId queueId, DynamicMessage messageTypeId, float queueMicroseconds, float processingMicroseconds) {
//			base.MessageProcessed(queueId, (int)messageTypeId, queueMicroseconds, processingMicroseconds);
//		}

//		public override void Transform(ref int messageTypeId) {
//			var enableDynamic = true;
//			if (!enableDynamic) {
//				messageTypeId = 0;
//				return;
//			}

//			//qq typically we may want to lump the rest together, but sometimes we might want to break them down by number
//			// to inform which message we should register next. this is dangerous in the sense that the numbers emitted
//			// for the typeids are not stable so never have this enabled when we might need to interpret the stats later
//			// and make some sense of them.
//			var aggregateDynamic = true;
//			if (aggregateDynamic) {
//				messageTypeId = (int)DynamicMessage.Other;
//			}
//		}
//	}
//}
