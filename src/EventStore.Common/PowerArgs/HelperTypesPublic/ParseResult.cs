using System.Collections.Generic;

namespace PowerArgs
{
    /// <summary>
    /// The raw parse result that contains the dictionary of values that were parsed
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// Dictionary of values that were either in the format -key value or /key:value on
        /// the command line.
        /// </summary>
        public Dictionary<string, string> ExplicitParameters { get; private set; }

        /// <summary>
        /// Dictionary of values that were implicitly specified by position where the key is the position (e.g. 0)
        /// and the value is the actual parameter value.
        /// 
        /// Example command line:  Program.exe John Smith
        /// 
        /// John would be an implicit parameter at position 0.
        /// Smith would be an implicit parameter at position 1.
        /// </summary>
        public Dictionary<int, string> ImplicitParameters { get; private set; }

        internal ParseResult()
        {
            ExplicitParameters = new Dictionary<string, string>();
            ImplicitParameters = new Dictionary<int, string>();
        }
    }
}
