namespace EventStore.Rags
{
    public static class NameTranslators
    {
        public static string PrefixEnvironmentVariables(string name, string prefix)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
            var convertedName = regex.Replace(name, "_");
            return prefix + convertedName.ToUpper();            
        }
    }
}