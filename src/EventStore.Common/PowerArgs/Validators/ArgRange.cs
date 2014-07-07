using System;

namespace PowerArgs
{
    /// <summary>
    /// Validates that the value is a number between the min and max (both inclusive) specified
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class ArgRange : ArgValidator
    {
        double min, max;

        /// <summary>
        /// Set to true if your max is exclusive.  This value is false by default.
        /// </summary>
        public bool MaxIsExclusive { get; set; }

        /// <summary>
        ///  Creates a new ArgRange validator.
        /// </summary>
        /// <param name="min">The minimum value (inclusive)</param>
        /// <param name="max">The maximum value (inclusive by default, set MaxIsExclusive to true to override)</param>
        public ArgRange(double min, double max)
        {
            this.min = min;
            this.max = max;
            this.MaxIsExclusive = false; // This could arguably have been true by default, but I shipped it this way so it will have to stay in case people are using it.
        }

        /// <summary>
        /// Validates that the value is a number between the min and max (both inclusive) specifie
        /// </summary>
        /// <param name="name">the name of the property being populated.  This validator doesn't do anything with it.</param>
        /// <param name="arg">The value specified on the command line</param>
        public override void Validate(string name, ref string arg)
        {
            double d;
            if (double.TryParse(arg, out d) == false)
            {
                throw new ValidationArgException("Expected a number for arg: " + name);
            }

            if (MaxIsExclusive == false)
            {
                if (d < min || d > max)
                {
                    throw new ValidationArgException(name + " must be at least " + min + ", but not greater than " + max, new ArgumentOutOfRangeException());
                }
            }
            else
            {
                if (d < min || d >= max)
                {
                    throw new ValidationArgException(name + " must be at least " + min + ", and less than " + max, new ArgumentOutOfRangeException());
                }
            }
        }
    }

}
