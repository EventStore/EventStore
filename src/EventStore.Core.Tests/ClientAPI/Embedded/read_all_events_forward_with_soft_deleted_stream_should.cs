using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded
{
    [Ignore("Metadata expected to fail in 3.9.4")]
    public class read_all_events_forward_with_soft_deleted_stream_should : ClientAPI.read_all_events_forward_with_soft_deleted_stream_should
    {
        protected override IEventStoreConnection BuildConnection(MiniNode node)
        {
            return EmbeddedTestConnection.To(node);
        }
    }
}