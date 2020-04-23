﻿using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using Microsoft.Extensions.Caching.Memory;

namespace EventStore.Core.Cluster {
	public class EventStoreClusterClientCache :
		IHandle<ClusterClientMessage.CleanCache>,
		IHandle<SystemMessage.SystemInit> {
		private readonly TimeSpan _cacheCleaningInterval = TimeSpan.FromMinutes(1);

		//TODO: Align with Gossip Dead Member Removal Time once the options is in ClusterNodeOptions
		private readonly TimeSpan _oldCacheItemThreshold = TimeSpan.FromMinutes(30);
		private readonly IPublisher _bus;
		private readonly Func<IPEndPoint, IPublisher, EventStoreClusterClient> _clientFactory;
		private readonly PublishEnvelope _publishEnvelope;
		private readonly MemoryCache _cache;

		public EventStoreClusterClientCache(IPublisher bus,
			Func<IPEndPoint, IPublisher, EventStoreClusterClient> clientFactory, TimeSpan? cacheCleaningInterval = null,
			TimeSpan? oldCacheItemThreshold = null) {
			_bus = bus ?? throw new ArgumentNullException(nameof(bus));
			_clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

			_cacheCleaningInterval = cacheCleaningInterval ?? _cacheCleaningInterval;
			_oldCacheItemThreshold = oldCacheItemThreshold ?? _oldCacheItemThreshold;

			_publishEnvelope = new PublishEnvelope(_bus);
			_cache = new MemoryCache(new MemoryCacheOptions {
				ExpirationScanFrequency = _oldCacheItemThreshold
			});
		}

		private void Start() {
			_bus.Publish(TimerMessage.Schedule.Create(_cacheCleaningInterval, _publishEnvelope,
				new ClusterClientMessage.CleanCache()));
		}

		public EventStoreClusterClient Get(IPEndPoint endpoint) {
			return _cache.GetOrCreate(endpoint, item => {
				item.SlidingExpiration = _oldCacheItemThreshold;
				item.RegisterPostEvictionCallback(callback: EvictionCallback);
				return _clientFactory(endpoint, _bus);
			});
		}

		private static void EvictionCallback(object key, object value, EvictionReason reason, object state) {
			if (value is IDisposable disposable) {
				disposable.Dispose();
			}
		}

		public void Handle(ClusterClientMessage.CleanCache message) {
			_cache.Compact(0);
			_bus.Publish(TimerMessage.Schedule.Create(_cacheCleaningInterval, _publishEnvelope,
				new ClusterClientMessage.CleanCache()));
		}

		public void Handle(SystemMessage.SystemInit message) {
			Start();
		}
	}
}
