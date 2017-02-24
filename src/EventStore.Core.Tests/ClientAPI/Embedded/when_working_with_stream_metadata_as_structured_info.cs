using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded
{
    [Ignore("Metadata expected to fail in 3.9.4")]
    public class when_working_with_stream_metadata_as_structured_info : ClientAPI.when_working_with_stream_metadata_as_structured_info
    {
        protected override IEventStoreConnection BuildConnection(MiniNode node)
        {
            return EmbeddedEventStoreConnection.Create(node.Node);
        }
    }
}
