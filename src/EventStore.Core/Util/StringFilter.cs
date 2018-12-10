using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EventStore.Core.Util
{
    /// <summary>
    /// This class can answer the question, if a given string is a member of a list of strings. 
    /// 
    /// Internally it uses different strategies based on the number of strings it has to compare. 
    /// </summary>
    public class StringFilter
    {
        private interface IFilterStrategy
        {
            bool IsStringAllowed(string s);
        }

        private readonly IFilterStrategy _strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:EventStore.Core.Util.StringFilter"/> class.
        /// </summary>
        /// <param name="allowedStrings">Allowed strings. If null or empty, all strings are considered valid by <see cref="IsStringAllowed(string)"/></param>
        public StringFilter(IReadOnlyList<string> allowedStrings)
        {
            if (allowedStrings == null || allowedStrings.Count == 0)
            {
                _strategy = new AlwaysAllowStrategy();
            }
            else if (allowedStrings.Count == 1)
            {
                _strategy = new SingleStringStrategy(allowedStrings[0]);
            }
            else
            {
                _strategy = new RegexStrategy(allowedStrings);
            }
        }

        /// <summary>
        /// Returns true, if the given string is part of the list of strings passed to the constructor. 
        /// 
        /// If the constructor was given an empty list, all strings are considered to be allowed. 
        /// </summary>
        /// <returns><c>true</c>, if string is allowed, <c>false</c> otherwise.</returns>
        /// <param name="s">String to check</param>
        public bool IsStringAllowed(string s)
        {
            return _strategy.IsStringAllowed(s);
        }

        private class AlwaysAllowStrategy : IFilterStrategy
        {
            public bool IsStringAllowed(string s)
            {
                return true;
            }
        }

        private class SingleStringStrategy : IFilterStrategy
        {
            private readonly string _expectedString;

            public SingleStringStrategy(string expectedString)
            {
                _expectedString = expectedString;
            }

            public bool IsStringAllowed(string s)
            {
                return _expectedString.Equals(s);
            }
        }

        private class RegexStrategy : IFilterStrategy
        {
            private readonly Regex _allowedStringsRegex;

            public RegexStrategy(IEnumerable<string> allowedStrings)
            {
                var filters = allowedStrings.Select(Regex.Escape);
                _allowedStringsRegex = new Regex("^" + string.Join("|", filters) + "$", RegexOptions.Compiled);
            }

            public bool IsStringAllowed(string s)
            {
                return _allowedStringsRegex.IsMatch(s);
            }
        }
    }
}
