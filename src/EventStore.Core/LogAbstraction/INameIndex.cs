namespace EventStore.Core.LogAbstraction {
	/// Maps names (strings) to TValues
	public interface INameIndex<TValue> {
		void CancelReservations();
		void CancelLastReservation();

		// return true => stream already existed.
		// return false => stream was created. addedValue and addedName are the details of the created stream.
		// these can be different to streamName/streamId e.g. if streamName is a metastream.
		// return null => no information available about the existence of the stream.
		bool? GetOrReserve(string name, out TValue value, out TValue addedValue, out string addedName);
	}
}
