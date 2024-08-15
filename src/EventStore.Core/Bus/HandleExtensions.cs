using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public static class HandleExtensions {
	public static IHandle<TInput> NarrowTo<TInput, TOutput>(this IHandle<TOutput> handler)
		where TInput : Message
		where TOutput : TInput {
		return new NarrowingHandler<TInput, TOutput>(handler);
	}
}
