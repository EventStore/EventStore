using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;
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
        private readonly ICheckpoint _checkpoint;
        private IList<IEventStoreService> _eventStoreServices = new List<IEventStoreService>();
        private bool _started;

        public PluginsHostService(IEventStoreServiceFactory factory, ICheckpoint checkpoint)
        {
            _serviceFactory = factory;
            _checkpoint = checkpoint;
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            if (message.State != VNodeState.Master && message.State != VNodeState.Clone &&
                message.State != VNodeState.Slave) return;
            if (_started)
                return;
            try
            {
                var t = new Thread(Start) { IsBackground = true };
                t.Start();
                _started = true;
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "Error on PluginsHostService");
            }
        }

        private async void Start()
        {
            if (_serviceFactory == null)
                return;
            _eventStoreServices = _serviceFactory.Create();
            foreach (var service in _eventStoreServices)
                if (service.AutoStart)
                {
                    await service.Start();
                    Log.Info($"Plugin '{service.Name}' started");
                }
        }

        public async Task<bool> TryPublish(IDictionary<string, dynamic> request)
        {
            // Go through all services to see if there is at least one handling it
            var result = false;
            foreach (var service in _eventStoreServices)
            {
                var res = await service.TryHandle(request);
                if (res)
                    result = true;
            }
            return result;
        }

        public void Handle(PluginMessage.GetStats message)
        {
            if (_serviceFactory == null)
                return;
            var results = _eventStoreServices.ToDictionary<IEventStoreService, string, dynamic>(
                eventStoreService => eventStoreService.Name, eventStoreService => eventStoreService.GetStats());
            results.Add("checkpoint", _checkpoint.Read());
            message.Envelope.ReplyWith(results.Count == 0
                ? new PluginMessage.GetStatsCompleted(PluginMessage.GetStatsCompleted.OperationStatus.NotReady, null)
                : new PluginMessage.GetStatsCompleted(PluginMessage.GetStatsCompleted.OperationStatus.Success,
                   JsonConvert.SerializeObject(results)));
        }
    }
}