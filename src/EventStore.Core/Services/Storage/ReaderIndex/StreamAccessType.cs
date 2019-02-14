namespace EventStore.Core.Services.Storage.ReaderIndex {
	public enum StreamAccessType {
		Read,
		Write,
		Delete,
		MetaRead,
		MetaWrite
	}
}
