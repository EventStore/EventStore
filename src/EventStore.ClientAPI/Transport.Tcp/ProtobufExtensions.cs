using System;
using System.IO;
using EventStore.ClientAPI.Common.Log;
using ProtoBuf;

namespace EventStore.ClientAPI.Transport.Tcp {
	internal static class ProtobufExtensions {
		private static readonly ILogger Log = new ConsoleLogger();

		public static T Deserialize<T>(this byte[] data) {
			return Deserialize<T>(new ArraySegment<byte>(data));
		}

		public static T Deserialize<T>(this ArraySegment<byte> data) {
			try {
				using (var memory = new MemoryStream(data.Array, data.Offset, data.Count)) {
					var res = Serializer.Deserialize<T>(memory);
					return res;
				}
			} catch (Exception e) {
				Log.Debug("Deserialization to {0} failed : {1}", typeof(T).FullName, e);
				return default(T);
			}
		}

		public static ArraySegment<byte> Serialize<T>(this T protoContract) {
			using (var memory = new MemoryStream()) {
				Serializer.Serialize(memory, protoContract);
				var res = new ArraySegment<byte>(memory.GetBuffer(), 0, (int)memory.Length);
				return res;
			}
		}

		public static byte[] SerializeToArray<T>(this T protoContract) {
			using (var memory = new MemoryStream()) {
				Serializer.Serialize(memory, protoContract);
				return memory.ToArray();
			}
		}
	}
}
