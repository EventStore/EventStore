using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Plugins;

namespace EventStore.Core.Services.Plugins
{
    public class PluginsHostControllerService :
        IHandle<SystemMessage.StateChangeMessage>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<PluginsHostControllerService>();
        private readonly IEventStoreControllerFactory _controllerFactory;
        private IList<IEventStoreController> _eventStoreControllers = new List<IEventStoreController>();

        public PluginsHostControllerService(IEventStoreControllerFactory factory)
        {
            _controllerFactory = factory;
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            if (message.State != VNodeState.Master && message.State != VNodeState.Clone &&
                message.State != VNodeState.Slave) return;
            try
            {
                var t = new Thread(Start) { IsBackground = true };
                t.Start();
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "Error on PluginsHostControllerService");
            }
        }

        private void Start()
        {
            if (_controllerFactory == null)
                return;
            _eventStoreControllers = _controllerFactory.Create();
        }
    }
}
