using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded
{
    [Ignore("Metadata expected to fail in 3.9.4")]
    public class when_working_with_stream_metadata_as_byte_array : ClientAPI.when_working_with_stream_metadata_as_byte_array
    {
        protected override IEventStoreConnection BuildConnection(MiniNode node)
        {
            return EmbeddedTestConnection.To(node);
        }
    }
}
