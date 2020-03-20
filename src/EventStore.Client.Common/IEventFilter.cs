namespace EventStore.Client {
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		interface IEventFilter {
		PrefixFilterExpression[] Prefixes { get; }
		RegularFilterExpression Regex { get; }
		uint? MaxSearchWindow { get; }
	}
}
