using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Methods for dealing with connection strings.
	/// </summary>
	public class ConnectionString {
		private static readonly Dictionary<Type, Func<string, object>> translators;

		static ConnectionString() {
			translators = new Dictionary<Type, Func<string, object>>() {
				{typeof(int), x => int.Parse(x, CultureInfo.InvariantCulture)},
				{typeof(decimal), x => decimal.Parse(x, CultureInfo.InvariantCulture)},
				{typeof(string), x => x},
				{typeof(bool), x => bool.Parse(x)},
				{typeof(long), x => long.Parse(x, CultureInfo.InvariantCulture)},
				{typeof(byte), x => byte.Parse(x, CultureInfo.InvariantCulture)},
				{typeof(double), x => double.Parse(x, CultureInfo.InvariantCulture)},
				{typeof(float), x => float.Parse(x, CultureInfo.InvariantCulture)},
				{typeof(TimeSpan), x => TimeSpan.FromMilliseconds(int.Parse(x, CultureInfo.InvariantCulture))}, {
					typeof(GossipSeed[]), x => x.Split(',').Select(q => {
						try {
							var pieces = q.Trim().Split(':');
							if (pieces.Length != 2) throw new Exception("Could not split IP address from port.");

							return new GossipSeed(new IPEndPoint(IPAddress.Parse(pieces[0]), int.Parse(pieces[1])));
						} catch (Exception ex) {
							throw new Exception(string.Format("Gossip seed {0} is not in correct format", q), ex);
						}
					}).ToArray()
				}, {
					typeof(UserCredentials), x => {
						try {
							var pieces = x.Trim().Split(':');
							if (pieces.Length != 2) throw new Exception("Could not split into username and password.");

							return new UserCredentials(pieces[0], pieces[1]);
						} catch (Exception ex) {
							throw new Exception(
								string.Format(
									"User credentials {0} is not in correct format. Expected format is username:password.",
									x), ex);
						}
					}
				}
			};
		}

		/// <summary>
		/// Parses a connection string into its pieces represented as kv pairs
		/// </summary>
		/// <param name="connectionString">the connection string to parse</param>
		/// <returns></returns>
		internal static IEnumerable<KeyValuePair<string, string>> GetConnectionStringInfo(string connectionString) {
			var builder = new DbConnectionStringBuilder(false) {ConnectionString = connectionString};
			//can someome mutate this builder before the enumerable is closed sure but thats the fun!
			return from object key in builder.Keys
				select new KeyValuePair<string, string>(key.ToString(), builder[key.ToString()].ToString());
		}

		/// <summary>
		/// Returns a <see cref="ConnectionSettings"></see> for a given connection string.
		/// </summary>
		/// <param name="connectionString">The connection string to parse</param>
		/// <param name="builder">Pre-populated settings builder, optional. If not specified, a new builder will be created.</param>
		/// <returns>a <see cref="ConnectionSettings"/> from the connection string</returns>
		public static ConnectionSettings GetConnectionSettings(string connectionString,
			ConnectionSettingsBuilder builder = null) {
			var settings = (builder ?? ConnectionSettings.Create()).Build();
			var items = GetConnectionStringInfo(connectionString).ToArray();
			return Apply(items, settings);
		}

		private static string WithSpaces(string name) {
			StringBuilder nameWithSpaces = new StringBuilder(32);
			nameWithSpaces.Append(name[0]);

			for (int i = 1; i < name.Length; i++) {
				char c = name[i];
				if (char.IsUpper(c)) {
					nameWithSpaces.Append(' ');
				}

				nameWithSpaces.Append(c);
			}

			return nameWithSpaces.ToString();
		}

		private static T Apply<T>(IEnumerable<KeyValuePair<string, string>> items, T obj) {
			var typeFields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public);

			var fields = typeFields.Select(x => new Tuple<string, FieldInfo>(x.Name, x))
				.Concat(typeFields.Select(x => new Tuple<string, FieldInfo>(WithSpaces(x.Name), x)))
				.GroupBy(x => x.Item1)
				.ToDictionary(x => x.First().Item1, x => x.First().Item2, StringComparer.InvariantCultureIgnoreCase);

			foreach (var item in items) {
				FieldInfo fi = null;
				if (!fields.TryGetValue(item.Key, out fi)) continue;
				Func<string, object> func = null;
				if (!translators.TryGetValue(fi.FieldType, out func)) {
					throw new Exception(string.Format("Can not map field named {0} as type {1} has no translator", item,
						fi.FieldType.Name));
				}

				fi.SetValue(obj, func(item.Value));
			}

			return obj;
		}
	}
}
