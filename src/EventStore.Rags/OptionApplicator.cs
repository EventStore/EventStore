using System.Collections.Generic;
using System.Linq;

namespace EventStore.Rags
{
    public class OptionApplicator
    {
        public static T Get<T>(IEnumerable<OptionSource> source) where T : class,new()
        {
            var revived = new T();
            var properties = revived.GetType().GetProperties();
            foreach (var option in source)
            {
                var property = properties.FirstOrDefault(x => x.Name == option.Name);
                if (property == null) continue;
                if (option.Value == null) continue;
                if (option.IsTyped)
                {
                    property.SetValue(revived, option.Value, null);
                }
                else
                {
                    object revivedValue = null;
                    //Let's make use of the ArgRevivers to do the heavy lifting of parsing the type, 
                    //so if it's an array, join it with a comma
                    if (option.Value.GetType().IsArray)
                    {
                        var commaJoined = string.Join(",", ((string[])option.Value));
                        revivedValue = TypeMap.Translate(property.PropertyType, option.Name, commaJoined);
                    }
                    else
                    {
                        revivedValue = TypeMap.Translate(property.PropertyType, option.Name, option.Value.ToString());
                    }
                    property.SetValue(revived, revivedValue, null);
                }
            }
            return revived;
        }
    }
}