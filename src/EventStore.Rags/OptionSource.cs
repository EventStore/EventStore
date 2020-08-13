namespace EventStore.Rags {
	public struct OptionSource {
		public string Source;
		public string Name;
		public bool IsTyped;
		public bool IsReference;
		public bool Mask;
		public bool Hidden;
		public object Value;

		public static OptionSource Typed(string source, string name, object value,
			bool isReference = false, bool mask = false, bool hidden = false) {
			return new OptionSource(source, name, true, value, isReference: isReference, mask: mask, hidden: hidden);
		}

		public static OptionSource String(string source, string name, object value,
			bool isReference = false, bool mask = false, bool hidden = false) {
			return new OptionSource(source, name, false, value, isReference: isReference, mask: mask, hidden: hidden);
		}

		public OptionSource(string source, string name, bool isTyped, object value,
			bool isReference = false, bool mask = false, bool hidden = false) {
			Source = source;
			Name = name;
			IsTyped = isTyped;
			Value = value;
			IsReference = isReference;
			Mask = mask;
			Hidden = hidden;
		}
	}
}
