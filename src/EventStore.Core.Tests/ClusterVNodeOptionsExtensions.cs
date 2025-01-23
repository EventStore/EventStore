// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests;

public static class ClusterVNodeOptionsExtensions {
	public static ClusterVNodeOptions ReduceMemoryUsageForTests(this ClusterVNodeOptions options) {
		return options with {
			Cluster = options.Cluster with {
				StreamInfoCacheCapacity = 10_000
			},
			Database = options.Database with {
				ChunkSize = MiniNode.ChunkSize,
				ChunksCacheSize = MiniNode.CachedChunkSize,
				StreamExistenceFilterSize = 10_000,
				ScavengeBackendCacheSize = 64 * 1024,
			}
		};
	}

	public static ClusterVNodeOptions RunInMemory(this ClusterVNodeOptions options) => options with {
		Database = options.Database with {
			MemDb = true,
			Db = new ClusterVNodeOptions().Database.Db
		}
	};

	public static ClusterVNodeOptions RunOnDisk(this ClusterVNodeOptions options, string path) => options with {
		Database = options.Database with {
			MemDb = false,
			Db = path
		}
	};

	public static ClusterVNodeOptions Secure(this ClusterVNodeOptions options,
		X509Certificate2Collection trustedRootCertificates, X509Certificate2 serverCertificate) => options with {
		Application = options.Application with {
			Insecure = false,
		},
		ServerCertificate = serverCertificate,
		TrustedRootCertificates = trustedRootCertificates
	};

	public static ClusterVNodeOptions Insecure(this ClusterVNodeOptions options) => options with {
		Application = options.Application with {
			Insecure = true
		},
		ServerCertificate = null,
		TrustedRootCertificates = null
	};

	public static ClusterVNodeOptions InCluster(this ClusterVNodeOptions options, int clusterSize) => options with {
		Cluster = options.Cluster with {
			ClusterSize = clusterSize <= 1
				? throw new ArgumentOutOfRangeException(nameof(clusterSize), clusterSize, $"{nameof(clusterSize)} must be greater than 1.")
				: clusterSize
		}
	};

	public static ClusterVNodeOptions WithGossipSeeds(this ClusterVNodeOptions options, EndPoint[] gossipSeeds) =>
		options with {
			Cluster = options.Cluster with {
				GossipSeed = gossipSeeds,
				DiscoverViaDns = false,
				ClusterDns = string.Empty
			}
		};

	public static ClusterVNodeOptions WithExternalTcpOn(
		this ClusterVNodeOptions options, IPEndPoint endPoint) =>
		options with {
			Interface = options.Interface with {
				NodeIp = endPoint.Address,
			}
		};

	public static ClusterVNodeOptions WithReplicationEndpointOn(
		this ClusterVNodeOptions options, IPEndPoint endPoint) =>
		options with {
			Interface = options.Interface with {
				ReplicationIp = endPoint.Address,
				ReplicationPort = endPoint.Port,
			}
		};

	public static ClusterVNodeOptions AdvertiseExternalHostAs(this ClusterVNodeOptions options, EndPoint endPoint) =>
		options with {
			Interface = options.Interface with {
				NodeHostAdvertiseAs = endPoint.GetHost(),
			}
		};

	public static ClusterVNodeOptions AdvertiseInternalHostAs(this ClusterVNodeOptions options, EndPoint endPoint) =>
		options with {
			Interface = options.Interface with {
				ReplicationHostAdvertiseAs = endPoint.GetHost(),
				ReplicationTcpPortAdvertiseAs = endPoint.GetPort()
			}
		};

	public static ClusterVNodeOptions AdvertiseNodeAs(this ClusterVNodeOptions options, EndPoint endPoint) =>
		options with {
			Interface = options.Interface with {
				NodeHostAdvertiseAs = endPoint.GetHost(),
				NodePortAdvertiseAs = endPoint.GetPort()
			}
		};

	public static ClusterVNodeOptions WithNodeEndpointOn(this ClusterVNodeOptions options, IPEndPoint endPoint) =>
		options with {
			Interface = options.Interface with {
				NodeIp = endPoint.Address,
				NodePort = endPoint.Port
			}
		};
}
