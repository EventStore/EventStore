namespace EventStore.Core.LogAbstraction.Common {
	public class LogV3ExistenceFilterReader : IExistenceFilterReader<uint> {
		public bool MightContain(uint item) => true;
	}
}
