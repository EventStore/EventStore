namespace EventStore.Core.LogAbstraction.Common {
	public class NoExistenceFilterReader : IExistenceFilterReader<uint> {
		public bool MightContain(uint item) => true;
	}
}
