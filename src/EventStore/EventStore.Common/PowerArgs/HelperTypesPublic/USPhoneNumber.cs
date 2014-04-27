using System.Text.RegularExpressions;

namespace PowerArgs
{
    /// <summary>
    /// An example of a custom type that uses regular expressions to extract values from the command line
    /// and implements an ArgReviver to transform the text input into a complex type.
    /// This class represents a US phone number.
    /// </summary>
    public class USPhoneNumber : GroupedRegexArg
    {
        /// <summary>
        /// The three digit area code of the phone number.
        /// </summary>
        public string AreaCode { get; private set; }

        /// <summary>
        /// The three digit first segment of the phone number
        /// </summary>
        public string FirstDigits { get; private set; }

        /// <summary>
        /// The four digit second segment of the phone number.
        /// </summary>
        public string SecondDigits { get; private set; }

        /// <summary>
        /// Creates a phone number object from a string
        /// </summary>
        /// <param name="phoneNumber"></param>
        public USPhoneNumber(string phoneNumber)
            : base(PhoneNumberRegex, phoneNumber, "Invalid phone number")
        {
            this.AreaCode = base["areaCode"];
            this.FirstDigits = base["firstDigits"];
            this.SecondDigits = base["secondDigits"];
        }

        /// <summary>
        /// Gets the default string representation of the phone number in the format '1-(aaa)-bbb-cccc'.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.ToString("1-({aaa})-{bbb}-{cccc}");
        }


        /// <summary>
        /// Formats the phone number as a string.  
        /// </summary>
        /// <param name="format">Use '{aaa}' for the area code, use {bbb} for the first grouping, and use {cccc} for the second grouping.</param>
        /// <returns>A formatted phone number string</returns>
        public string ToString(string format)
        {
            return format.Replace("{aaa}", this.AreaCode)
                        .Replace("{bbb}", this.FirstDigits)
                        .Replace("{cccc}", this.SecondDigits);
        }

        /// <summary>
        /// Custom PowerArgs reviver that converts a string parameter into a custom phone number
        /// </summary>
        /// <param name="key">The name of the argument (not used)</param>
        /// <param name="val">The value specified on the command line</param>
        /// <returns>A USPhoneNumber object based on the user input</returns>
        [ArgReviver]
        public static USPhoneNumber Revive(string key, string val)
        {
            return new USPhoneNumber(val);
        }

        private static string PhoneNumberRegex
        {
            get
            {
                // Helpers
                string optionalDash = Group("-?");
                string openParen = Regex.Escape("(");
                string closeParen = Regex.Escape(")");

                // Phone numbers can start with 1- or 1
                string optionalPrefix = Group(@"(1-|1)?");                                

                // Area code
                string areaCodeOption1 = Group(@"[2-9]\d{2}", "areaCode");                // Area code is three digits, where the first digit cannot be 0 or 1
                string areaCodeOption2 = Group(openParen + areaCodeOption1 + closeParen); // Area code can optionally be enclosed in parentheses
                string areaCode = Group(areaCodeOption1 + "|" + areaCodeOption2);

                // Final groups
                string firstDigitGroup = Group(@"\d{3}", "firstDigits");                  // First 3 digit grouping
                string secondDigitGroup = Group(@"\d{4}", "secondDigits");                // Second 4 digit grouping

                // Composite
                string ret = optionalPrefix + areaCode + optionalDash + firstDigitGroup + optionalDash + secondDigitGroup;
                return ret;
            }
        }
    }
}
