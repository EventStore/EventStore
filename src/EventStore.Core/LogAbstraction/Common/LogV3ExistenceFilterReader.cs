namespace EventStore.Core.LogAbstraction.Common {
	public class LogV3ExistenceFilterReader : IExistenceFilterReader<uint> {
		public bool MightExist(uint item) => true;
	}
}
