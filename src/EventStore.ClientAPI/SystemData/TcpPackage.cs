using System;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Internal;

namespace EventStore.ClientAPI.SystemData {
	[Flags]
	internal enum TcpFlags : byte {
		None = 0x00,
		Authenticated = 0x01,
	}

	internal struct TcpPackage {
		public const int CommandOffset = 0;
		public const int FlagsOffset = CommandOffset + 1;
		public const int CorrelationOffset = FlagsOffset + 1;
		public const int AuthOffset = CorrelationOffset + 16;

		public const int MandatorySize = AuthOffset;

		public readonly TcpCommand Command;
		public readonly TcpFlags Flags;
		public readonly Guid CorrelationId;
		public readonly string Login;
		public readonly string Password;
		public readonly ArraySegment<byte> Data;

		public static TcpPackage FromArraySegment(ArraySegment<byte> data) {
			if (data.Count < MandatorySize)
				throw new ArgumentException(string.Format("ArraySegment too short, length: {0}", data.Count), "data");

			var command = (TcpCommand)data.Array[data.Offset + CommandOffset];
			var flags = (TcpFlags)data.Array[data.Offset + FlagsOffset];

			var guidBytes = new byte[16];
			Buffer.BlockCopy(data.Array, data.Offset + CorrelationOffset, guidBytes, 0, 16);
			var correlationId = new Guid(guidBytes);

			var headerSize = MandatorySize;
			string login = null;
			string pass = null;
			if ((flags & TcpFlags.Authenticated) != 0) {
				var loginLen = data.Array[data.Offset + AuthOffset];
				if (AuthOffset + 1 + loginLen + 1 >= data.Count)
					throw new Exception("Login length is too big, it does not fit into TcpPackage.");
				login = Helper.UTF8NoBom.GetString(data.Array, data.Offset + AuthOffset + 1, loginLen);

				var passLen = data.Array[data.Offset + AuthOffset + 1 + loginLen];
				if (AuthOffset + 1 + loginLen + 1 + passLen > data.Count)
					throw new Exception("Password length is too big, it does not fit into TcpPackage.");
				pass = Helper.UTF8NoBom.GetString(data.Array, data.Offset + AuthOffset + 1 + loginLen + 1, passLen);

				headerSize += 1 + loginLen + 1 + passLen;
			}

			return new TcpPackage(command,
				flags,
				correlationId,
				login,
				pass,
				new ArraySegment<byte>(data.Array, data.Offset + headerSize, data.Count - headerSize));
		}

		public TcpPackage(TcpCommand command, Guid correlationId, byte[] data)
			: this(command, TcpFlags.None, correlationId, null, null, data) {
		}

		public TcpPackage(TcpCommand command, Guid correlationId, ArraySegment<byte> data)
			: this(command, TcpFlags.None, correlationId, null, null, data) {
		}

		public TcpPackage(TcpCommand command, TcpFlags flags, Guid correlationId, string login, string password,
			byte[] data)
			: this(command, flags, correlationId, login, password, new ArraySegment<byte>(data ?? Empty.ByteArray)) {
		}

		public TcpPackage(TcpCommand command, TcpFlags flags, Guid correlationId, string login, string password,
			ArraySegment<byte> data) {
			if ((flags & TcpFlags.Authenticated) != 0) {
				Ensure.NotNull(login, "login");
				Ensure.NotNull(password, "password");
			} else {
				if (login != null) throw new ArgumentException("Login provided for non-authorized TcpPackage.");
				if (password != null) throw new ArgumentException("Password provided for non-authorized TcpPackage.");
			}

			Command = command;
			Flags = flags;
			CorrelationId = correlationId;
			Login = login;
			Password = password;
			Data = data;
		}

		public byte[] AsByteArray() {
			if ((Flags & TcpFlags.Authenticated) != 0) {
				var loginLen = Helper.UTF8NoBom.GetByteCount(Login);
				var passLen = Helper.UTF8NoBom.GetByteCount(Password);
				if (loginLen > 255)
					throw new ArgumentException(
						string.Format("Login serialized length should be less than 256 bytes (but is {0}).", loginLen));
				if (passLen > 255)
					throw new ArgumentException(
						string.Format("Password serialized length should be less than 256 bytes (but is {0}).",
							passLen));

				var res = new byte[MandatorySize + 2 + loginLen + passLen + Data.Count];
				res[CommandOffset] = (byte)Command;
				res[FlagsOffset] = (byte)Flags;
				Buffer.BlockCopy(CorrelationId.ToByteArray(), 0, res, CorrelationOffset, 16);

				res[AuthOffset] = (byte)loginLen;
				Helper.UTF8NoBom.GetBytes(Login, 0, Login.Length, res, AuthOffset + 1);
				res[AuthOffset + 1 + loginLen] = (byte)passLen;
				Helper.UTF8NoBom.GetBytes(Password, 0, Password.Length, res, AuthOffset + 1 + loginLen + 1);

				Buffer.BlockCopy(Data.Array, Data.Offset, res, res.Length - Data.Count, Data.Count);
				return res;
			} else {
				var res = new byte[MandatorySize + Data.Count];
				res[CommandOffset] = (byte)Command;
				res[FlagsOffset] = (byte)Flags;
				Buffer.BlockCopy(CorrelationId.ToByteArray(), 0, res, CorrelationOffset, 16);
				Buffer.BlockCopy(Data.Array, Data.Offset, res, res.Length - Data.Count, Data.Count);
				return res;
			}
		}

		public ArraySegment<byte> AsArraySegment() {
			return new ArraySegment<byte>(AsByteArray());
		}
	}
}
