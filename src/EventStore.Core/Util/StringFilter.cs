using System;
using System.Text;
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
        private interface CompareStrategy
        {
            bool IsStringAllowed(String s);
        }

        private CompareStrategy strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:EventStore.Core.Util.StringFilter"/> class.
        /// </summary>
        /// <param name="allowedStrings">Allowed strings. If null or empty, all strings are considered valid by <see cref="IsStringAllowed(string)"/></param>
        public StringFilter(string[] allowedStrings)
        {
            if(allowedStrings == null || allowedStrings.Length == 0)
            {
                this.strategy = new AlwaysAllowStrategy();
            } 
            else if(allowedStrings.Length == 1)
            {
                this.strategy = new SingleStringStrategy(allowedStrings[0]);
            }
            else
            {
                this.strategy = new RegexStrategy(allowedStrings);
            }
        }

        /// <summary>
        /// Returns true, if the given string is part of the list of strings passed to the constructor. 
        /// 
        /// If the constructor was given an empty list, all strings are considered to be allowed. 
        /// </summary>
        /// <returns><c>true</c>, if string is allowed, <c>false</c> otherwise.</returns>
        /// <param name="s">String to check</param>
        public bool IsStringAllowed(String s)
        {
            return this.strategy.IsStringAllowed(s);
        }

        private class AlwaysAllowStrategy : CompareStrategy
        {
            public bool IsStringAllowed(string s)
            {
                return true;
            }
        }

        private class SingleStringStrategy : CompareStrategy
        {
            private readonly String expectedString;

            public SingleStringStrategy(String expectedString) => this.expectedString = expectedString;

            public bool IsStringAllowed(string s)
            {
                return this.expectedString.Equals(s);
            }
        }

        private class RegexStrategy : CompareStrategy
        {
            private readonly Regex allowedStringsRegex;

            public RegexStrategy(string[] allowedStrings)
            {
                bool first = true;
                StringBuilder sb = new StringBuilder();
                sb.Append("^");
                foreach (string s in allowedStrings)
                {
                    if (!first)
                    {
                        sb.Append("|");
                    }
                    first = false;
                    sb.Append(Regex.Escape(s));
                }
                sb.Append("$");
                this.allowedStringsRegex = new Regex(sb.ToString(), RegexOptions.Compiled);
            }

            public bool IsStringAllowed(string s)
            {
                return this.allowedStringsRegex.IsMatch(s);
            }
        }
    }

}
