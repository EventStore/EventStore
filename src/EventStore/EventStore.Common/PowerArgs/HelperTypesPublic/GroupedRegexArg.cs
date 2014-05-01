using System.Linq;
using System.Text.RegularExpressions;

namespace PowerArgs
{
    /// <summary>
    /// An abstract class that lets you create custom argument types that match a regular expression.  The 
    /// class also makes it easy to extract named groups from the regular expression for use by your application.
    /// </summary>
    public abstract class GroupedRegexArg
    {
        /// <summary>
        /// The match that exactly matches the given regular expression
        /// </summary>
        protected Match exactMatch;

        /// <summary>
        /// Creates a new grouped regular expression argument instance.
        /// </summary>
        /// <param name="regex">The regular expression to enforce</param>
        /// <param name="input">The user input that was provided</param>
        /// <param name="errorMessage">An error message to show in the case of a non match</param>
        protected GroupedRegexArg(string regex, string input, string errorMessage)
        {
            MatchCollection matches = Regex.Matches(input, regex);
            exactMatch = (from m in matches.ToList() where m.Value == input select m).SingleOrDefault();
            if (exactMatch == null) throw new ArgException(errorMessage + ": " + input);
        }

        /// <summary>
        /// A helper function you can use to group a particular regular expression.
        /// </summary>
        /// <param name="regex">Your regular expression that you would like to put in a group.</param>
        /// <param name="groupName">The name of the group that you can use to extract the group value later.</param>
        /// <returns></returns>
        protected static string Group(string regex, string groupName = null)
        {
            return groupName == null ? "(" + regex + ")" : "(?<" + groupName + ">" + regex + ")";
        }

        /// <summary>
        /// Gets the value of the regex group from the exact match.
        /// </summary>
        /// <param name="groupName">The name of the group to lookup</param>
        /// <returns></returns>
        protected string this[string groupName]
        {
            get
            {
                return this.exactMatch.Groups[groupName].Value;
            }
        }

        /// <summary>
        /// Gets the value of the regex group from the exact match.
        /// </summary>
        /// <param name="groupNumber">The index of the group to lookup</param>
        /// <returns></returns>
        protected string this[int groupNumber]
        {
            get
            {
                return this.exactMatch.Groups[groupNumber].Value;
            }
        }
    }
}
