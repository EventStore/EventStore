using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;

namespace EventStore.Rags {
	public class Translators {
		public static bool TranslateBool(string prop, string val) {
			return val != null && val.ToLower() != "false" && val != "0";
		}

		public static IPEndPoint TranslateIPEndPoint(string prop, string val) {
			var parts = val.Split(':');
			return new IPEndPoint(IPAddress.Parse(parts[0]), Int32.Parse(parts[1]));
		}

		public static IPAddress TranlateIPAddress(string prop, string val) {
			return IPAddress.Parse(val);
		}

		public static Uri TranslateUri(string prop, string val) {
			try {
				return new Uri(val);
			} catch (UriFormatException) {
				throw new UriFormatException("value must be a valid URI: " + val);
			}
		}

		public static DateTime TranslateDateTime(string prop, string val) {
			DateTime ret;
			if (DateTime.TryParse(val, out ret) == false)
				throw new FormatException(String.Format("value for {0} must be a valid date time: {1}", prop, val));
			return ret;
		}

		public static double TranslateDouble(string prop, string val) {
			double ret;
			if (double.TryParse(val, out ret) == false)
				throw new FormatException(String.Format("value for {0} must be a number: {1}", prop, val));
			return ret;
		}

		public static long TranslateLong(string prop, string val) {
			long ret;
			if (long.TryParse(val, out ret) == false)
				throw new FormatException(String.Format("value for {0} must be an integer: {1}", prop, val));
			return ret;
		}

		public static int TranslateInt(string prop, string val) {
			int ret;
			if (int.TryParse(val, out ret) == false)
				throw new FormatException(String.Format("value for {0} must be an integer: {1}", prop, val));
			return ret;
		}

		public static byte TranslateByte(string prop, string val) {
			byte ret;
			if (byte.TryParse(val, out ret) == false)
				throw new FormatException(String.Format("value for {0} must be a byte: {1}", prop, val));
			return ret;
		}

		public static Guid TranslateGuid(string prop, string val) {
			Guid ret;
			if (Guid.TryParse(val, out ret) == false)
				throw new FormatException(String.Format("value for {0} must be a Guid: {1}", prop, val));
			return ret;
		}
	}
}
