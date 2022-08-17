using System;
using System.Linq;
using System.Runtime.CompilerServices;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public class MemSizer {
		public const int ObjectHeaderSize = 16;
		public const int ArraySize = 24;
		public const int StringSize = 20;

		public static int SizeOf(string s)
		{
			if (s.Length == 0)
				return 0;
			return (StringSize + s.Length * 2 + 1).RoundUpToMultipleOf(IntPtr.Size);
		}

		public static int SizeOf(string[] arr)
		{
			if (arr == null)
				return 0;

			var size = 0;
			size += ArraySize; // string array
			size += arr.Length * Unsafe.SizeOf<string>(); // string refs
			size += arr.Sum(SizeOf); // strings
			return size;
		}
	}
}
