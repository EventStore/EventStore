using System.IO;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Exceptions;

namespace EventStore.Core.Index {
	public class PTableHeader {
		public const int Size = 128;

		public readonly FileType FileType;
		public readonly byte Version;

		public PTableHeader(byte version) {
			FileType = FileType.PTableFile;
			Version = version;
		}

		public byte[] AsByteArray() {
			var array = new byte[Size];
			array[0] = (byte)FileType.PTableFile;
			array[1] = Version;
			return array;
		}

		public static PTableHeader FromStream(Stream stream) {
			var type = stream.ReadByte();
			if (type != (int)FileType.PTableFile)
				throw new CorruptIndexException("Corrupted PTable.", new InvalidFileException("Wrong type of PTable."));
			var version = stream.ReadByte();
			if (version == -1)
				throw new CorruptIndexException("Couldn't read version of PTable from header.",
					new InvalidFileException("Invalid PTable file."));
			return new PTableHeader((byte)version);
		}
	}
}
