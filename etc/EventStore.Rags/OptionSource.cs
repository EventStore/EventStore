namespace EventStore.Rags
{
    public struct OptionSource 
    {
        public string Source;
        public string Name;
        public bool IsTyped;
        public object Value;

        public static OptionSource Typed(string source, string name, object value)
        {
            return new OptionSource(source, name, true, value);
        }

        public static OptionSource String(string source, string name, object value)
        {
            return new OptionSource(source, name, false, value);
        }

        public OptionSource(string source, string name, bool isTyped, object value)
        {
            Source = source;
            Name = name;
            IsTyped = isTyped;
            Value = value;
        }
    }
}