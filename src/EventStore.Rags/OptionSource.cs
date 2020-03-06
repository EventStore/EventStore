namespace EventStore.Rags {
	public struct OptionSource {
		public string Source;
		public string Name;
		public bool IsTyped;
		public bool IsReference;
		public bool Mask;
		public object Value;

		public static OptionSource Typed(string source, string name, object value,
			bool isReference = false, bool mask = false) {
			return new OptionSource(source, name, true, value, isReference: isReference, mask: mask);
		}

		public static OptionSource String(string source, string name, object value,
			bool isReference = false, bool mask = false) {
			return new OptionSource(source, name, false, value, isReference: isReference, mask: mask);
		}

		public OptionSource(string source, string name, bool isTyped, object value,
			bool isReference = false, bool mask = false) {
			Source = source;
			Name = name;
			IsTyped = isTyped;
			Value = value;
			IsReference = isReference;
			Mask = mask;
		}
	}
}
