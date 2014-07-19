using System.IO;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Util;

namespace EventStore.Web.Users
{
    public sealed class UsersWebController : CommunicationController
    {
        private readonly MiniWeb _miniWeb;

        public UsersWebController(IPublisher publisher): base(publisher)
        {
            string nodeFSRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Web");
            _miniWeb = new MiniWeb("/web/users", Path.Combine(nodeFSRoot, "Users", "web"));
        }

        protected override void SubscribeCore(IHttpService service)
        {
            _miniWeb.RegisterControllerActions(service);
        }
    }
}
