namespace EventStore.Core.Diagnostics {
	//qqq i wonder whether these still need to be used as indexes into arrays (nope)
	//    because if not we can do handy masking with them..... although we would probably need a separate
	//    enum to generate the manifest with
	//    it might depend on how much flexibility we need in here and how much the aggregation should just be done
	//    in the consuming tool
	//qq relocate, are there any others that should be in here too?
	[StatsGroup("core-misc")]
	public enum MessageType : int {
		None = 0,
		Schedule = 1,
		//ExampleBase,
		//Example,
		//Example2,
		//Example3,

		////qq <-- could specify flags on them like this Reads flag, but we would
		//// have to bear in mind that you cant change the flags without changing the identity of the message.
		//// or we make the identity if the message one part and the flags another part, and have a mask that
		//// determines which we are paying attention to
		//// they flags might have a relationship with the tags (?) in the eventsource?
		//// but the flags could be useful, we might set a mask
		//ReadStreamEventsForward = 2 | Reads,
		//SendViewChangeProof,
		//Gossip,
		//WritePrepares,

		//Reads = 1000,
		////qq now, add here a message that we want to handle in a custom way


		////qqq relies on not having to use these as array indexes, but i think the only place we _might_
		//// is in the TryTransform specific to this enum, so it can be up to the plugin whether
		//// it sticks groups like 'other' at the end or in the middle.
		//Other = int.MaxValue,
	}
}
