namespace EventStore.Rags
{
    public struct OptionSource 
    {
        public string Source;
        public string Name;
        public object Value;

        public OptionSource(string source, string name, object value)
        {
            this.Source = source;
            this.Name = name;
            this.Value = value;
        }
    }
}