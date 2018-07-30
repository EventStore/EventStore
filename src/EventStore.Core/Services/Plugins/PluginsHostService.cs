using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Plugins;
using Newtonsoft.Json;

namespace EventStore.Core.Services.Plugins
{
    public class PluginsHostService :
        IHandle<SystemMessage.StateChangeMessage>,
        IHandle<PluginMessage.GetStats>,
        IPluginPublisher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<PluginsHostService>();
        private readonly IEventStoreServiceFactory _serviceFactory;
        private IList<IEventStoreService> _eventStoreServices = new List<IEventStoreService>();

        public PluginsHostService(IEventStoreServiceFactory factory)
        {
            _serviceFactory = factory;
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
                Log.ErrorException(e, "Error on PluginsHostService");
            }
        }

        private void Start()
        {
            if (_serviceFactory == null)
                return;
            _eventStoreServices = _serviceFactory.Create();
            foreach (var service in _eventStoreServices)
                if (service.AutoStart)
                {
                    service.Start();
                    Log.Info($"Plugin '{service.Name}' started");
                }
        }

        public bool TryPublish(IDictionary<string, dynamic> request)
        {
            var result = false;
            foreach (var service in _eventStoreServices)
                if (service.TryHandle(request))
                    result = true;
            return result;
        }

        public void Handle(PluginMessage.GetStats message)
        {
            if (_serviceFactory == null)
                return;
            var results = _eventStoreServices.ToDictionary<IEventStoreService, string, dynamic>(
                eventStoreService => eventStoreService.Name, eventStoreService => eventStoreService.GetStats());
            message.Envelope.ReplyWith(results.Count == 0
                ? new PluginMessage.GetStatsCompleted(PluginMessage.GetStatsCompleted.OperationStatus.NotReady, null)
                : new PluginMessage.GetStatsCompleted(PluginMessage.GetStatsCompleted.OperationStatus.Success,
                   JsonConvert.SerializeObject(results)));
        }
    }
}
