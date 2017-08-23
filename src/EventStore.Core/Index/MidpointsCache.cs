using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using static EventStore.Core.Index.PTable;

namespace EventStore.Core.Index
{
    public static class MidpointsCache
    {
        public static bool Exists(string filename)
        {
            return File.Exists(filename);
        }
        public static void Write(string filename, Midpoint[] midpoints)
        {
            if (midpoints == null || midpoints.Length == 0) return;

            var fs = new FileStream(filename, FileMode.Create);
            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, midpoints);
            }
            catch (SerializationException e)
            {
                Console.WriteLine("Failed to serialize. Reason: " + e.Message);
                throw;
            }
            finally
            {
                fs.Close();
            }
        }

        public static Midpoint[] Read(string filename)
        {
            Midpoint[] midpoints;
            FileStream fs = new FileStream(filename, FileMode.Open);
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();

                midpoints = (Midpoint[])formatter.Deserialize(fs);
            }
            catch (SerializationException e)
            {
                Console.WriteLine("Failed to deserialize. Reason: " + e.Message);
                throw;
            }
            finally
            {
                fs.Close();
            }
            return midpoints;
        }
    }
}
