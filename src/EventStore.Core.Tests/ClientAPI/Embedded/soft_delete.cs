using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Embedded
{
    [Ignore("Metadata expected to fail in 3.9.4")]
    public class soft_delete : ClientAPI.soft_delete
    {
        protected override IEventStoreConnection BuildConnection(MiniNode node)
        {
            return EmbeddedTestConnection.To(node);
        }
    }
}