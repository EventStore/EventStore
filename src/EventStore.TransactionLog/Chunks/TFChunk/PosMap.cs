using System.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	public struct PosMap {
		public const int FullSize = sizeof(long) + sizeof(int);
		public const int DeprecatedSize = sizeof(int) + sizeof(int);

		public readonly long LogPos;
		public readonly int ActualPos;

		public PosMap(long logPos, int actualPos) {
			LogPos = logPos;
			ActualPos = actualPos;
		}

		public static PosMap FromNewFormat(BinaryReader reader) {
			var actualPos = reader.ReadInt32();
			var logPos = reader.ReadInt64();
			return new PosMap(logPos, actualPos);
		}

		public static PosMap FromOldFormat(BinaryReader reader) {
			var posmap = reader.ReadUInt64();
			var logPos = (int)(posmap >> 32);
			var actualPos = (int)(posmap & 0xFFFFFFFF);
			return new PosMap(logPos, actualPos);
		}

		public void Write(BinaryWriter writer) {
			writer.Write(ActualPos);
			writer.Write(LogPos);
		}

		public override string ToString() {
			return string.Format("LogPos: {0}, ActualPos: {1}", LogPos, ActualPos);
		}
	}
}
