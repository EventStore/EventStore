using System.Collections.Generic;

namespace EventStore.Core.LogAbstraction {
	public interface INameEnumerator<TCheckpoint> {
		/// <summary>
		/// Enumerates names and their corresponding positions in log insertion order
		/// A name may appear more than once depending on the implementation
		/// </summary>
		/// <param name="lastCheckpoint">Position to read from, exclusive</param>
		/// <returns></returns>
		IEnumerable<(string name, TCheckpoint checkpoint)> EnumerateNames(TCheckpoint lastCheckpoint);
	}
}
