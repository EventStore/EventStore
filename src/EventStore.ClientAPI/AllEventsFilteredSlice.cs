using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    public class AllEventsFilteredSlice : AllEventsSlice
    {
        private readonly bool IsEndOfStreamInternal;

        public override bool IsEndOfStream { get { return IsEndOfStreamInternal; } }

        internal AllEventsFilteredSlice(ReadDirection readDirection, Position fromPosition, Position nextPosition, ClientMessage.ResolvedEvent[] events, bool isEndOfStream) 
            : base(readDirection, fromPosition, nextPosition, events)
        {
            IsEndOfStreamInternal = isEndOfStream;
        }
    }
}
