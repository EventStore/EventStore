using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;

namespace EventStore.Core {
	public static class ClusterVNodeOptionsExtensions {
		public static ClusterVNodeOptions InCluster(this ClusterVNodeOptions options, int clusterSize) => options with {
			Cluster = options.Cluster with {
				ClusterSize = clusterSize <= 1
					? throw new ArgumentOutOfRangeException(nameof(clusterSize), clusterSize,  $"{nameof(clusterSize)} must be greater than 1.")
					: clusterSize
			}
		};
		/// <summary>
		/// Returns a builder set to run in memory only
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions RunInMemory(this ClusterVNodeOptions options) => options with {
			Database = options.Database with {
				MemDb = true,
				Db = new ClusterVNodeOptions().Database.Db
			}
		};

		/// <summary>
		/// Returns a builder set to write database files to the specified path
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="path">The path on disk in which to write the database files</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions RunOnDisk(this ClusterVNodeOptions options, string path) => options with {
			Database = options.Database with {
				MemDb = false,
				Db = path
			}
		};

		/// <summary>
		/// Runs the node in insecure mode.
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions Insecure(this ClusterVNodeOptions options) => options with {
			Application = options.Application with {
				Insecure = true
			},
			Interface = options.Interface with {
				DisableExternalTcpTls = true
			},
			ServerCertificate = null,
			TrustedRootCertificates = null
		};

		/// <summary>
		/// Runs the node in secure mode.
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="trustedRootCertificates">A <see cref="X509Certificate2Collection"/> containing trusted root <see cref="X509Certificate2"/></param>
		/// <param name="serverCertificate">A <see cref="X509Certificate2"/> for the server</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions Secure(this ClusterVNodeOptions options,
			X509Certificate2Collection trustedRootCertificates, X509Certificate2 serverCertificate) => options with {
			Application = options.Application with {
				Insecure = false,
			},
			Interface = options.Interface with {
				DisableExternalTcpTls = false
			},
			ServerCertificate = serverCertificate,
			TrustedRootCertificates = trustedRootCertificates
		};

		/// <summary>
		/// Sets gossip seeds to the specified value and turns off dns discovery
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="gossipSeeds">The list of gossip seeds</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions WithGossipSeeds(this ClusterVNodeOptions options, EndPoint[] gossipSeeds) =>
			options with {
				Cluster = options.Cluster with {
					GossipSeed = gossipSeeds,
					DiscoverViaDns = false,
					ClusterDns = string.Empty
				}
			};

		/// <summary>
		/// Sets the external tcp endpoint to the specified value
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The external secure endpoint to use</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions WithExternalSecureTcpOn(
			this ClusterVNodeOptions options, IPEndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					EnableExternalTcp = true,
					ExtIp = endPoint.Address,
					DisableExternalTcpTls = false,
					ExtTcpPort = endPoint.Port
				}
			};

		/// <summary>
		/// Sets the internal secure tcp endpoint to the specified value
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The internal secure endpoint to use</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions WithInternalSecureTcpOn(
			this ClusterVNodeOptions options, IPEndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					IntIp = endPoint.Address,
					DisableInternalTcpTls = false,
					IntTcpPort = endPoint.Port
				}
			};

		/// <summary>
		/// Sets the external tcp endpoint to the specified value
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The external endpoint to use</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions WithExternalTcpOn(
			this ClusterVNodeOptions options, IPEndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					EnableExternalTcp = true,
					ExtIp = endPoint.Address,
					ExtTcpPort = endPoint.Port,
					DisableExternalTcpTls = true
				}
			};

		/// <summary>
		/// Sets the internal tcp endpoint to the specified value
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The internal endpoint to use</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions WithInternalTcpOn(
			this ClusterVNodeOptions options, IPEndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					IntIp = endPoint.Address,
					IntTcpPort = endPoint.Port,
					DisableInternalTcpTls = true
				}
			};

		/// <summary>
		/// Sets the http endpoint to the specified value
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The http endpoint to use</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions WithHttpOn(
			this ClusterVNodeOptions options, IPEndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					ExtIp = endPoint.Address,
					HttpPort = endPoint.Port
				}
			};

		/// <summary>
		/// Sets up the External Host that would be advertised
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The advertised host</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions
			AdvertiseExternalHostAs(this ClusterVNodeOptions options, EndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					ExtHostAdvertiseAs = endPoint.GetHost(),
					ExtTcpPortAdvertiseAs = endPoint.GetPort()
				}
			};

		/// <summary>
		/// Sets up the Internal Host that would be advertised
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The advertised host</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions
			AdvertiseInternalHostAs(this ClusterVNodeOptions options, EndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					IntHostAdvertiseAs = endPoint.GetHost(),
					IntTcpPortAdvertiseAs = endPoint.GetPort()
				}
			};

		/// <summary>
		/// </summary>
		/// <param name="options">The <see cref="ClusterVNodeOptions"/></param>
		/// <param name="endPoint">The advertised host</param>
		/// <returns>A <see cref="ClusterVNodeOptions"/> with the options set</returns>
		public static ClusterVNodeOptions AdvertiseHttpHostAs(this ClusterVNodeOptions options, EndPoint endPoint) =>
			options with {
				Interface = options.Interface with {
					ExtHostAdvertiseAs = endPoint.GetHost(),
					HttpPortAdvertiseAs = endPoint.GetPort()
				}
			};
	}
}
