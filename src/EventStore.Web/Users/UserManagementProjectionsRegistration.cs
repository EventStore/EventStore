using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Web.Users
{
    public sealed class UserManagementProjectionsRegistration :
        IHandle<ProjectionManagementMessage.RequestSystemProjections>
    {
        public void Handle(ProjectionManagementMessage.RequestSystemProjections message)
        {
            var handlerType = typeof (IndexUsersProjectionHandler);
            var handler = "native:" + handlerType.Namespace + "." + handlerType.Name;
            message.Envelope.ReplyWith(
                new ProjectionManagementMessage.RegisterSystemProjection("$users", handler, ""));
        }
    }
}
