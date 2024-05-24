using System.Security.Cryptography;
using EventStore.Plugins;
using EventStore.Plugins.MD5;

namespace EventStore.Core.Hashing;

public class NetMD5Provider : Plugin, IMD5Provider {
	public HashAlgorithm Create() => System.Security.Cryptography.MD5.Create();
}
