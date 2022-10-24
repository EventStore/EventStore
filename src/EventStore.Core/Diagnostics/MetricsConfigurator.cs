using System;
using System.Reflection;
using EventStore.Core.Messaging;
using Serilog;

namespace EventStore.Core.Diagnostics {
	public class MetricsConfigurator {
		private static readonly ILogger Log = Serilog.Log.ForContext<MetricsConfigurator>();

		//qq this takes the group configuration and uses it to modify the stats ids on the appropriate messages.
		//qq prolly add some logging.
		public static void Configure(MetricsConfiguration configuration) {
			//qq we configure all the groups by iterating through the messagehierarchy only once
			// for each messages type we find out which group it is in and what its id is, then we look to see if
			// we have an override id for that in the configuration. if so, set it.
			foreach (var kvp in MessageHierarchy.MsgTypeIdByType) {
				ConfigureMessageType(kvp.Key);
			}

			void ConfigureMessageType(Type messageType) {
				if (messageType.IsAbstract)
					return;

				PropertyInfo GetPublicStaticProperty(string name) {
					return messageType.GetProperty(name, BindingFlags.Static | BindingFlags.Public);
				}

				var eventSourceProperty = GetPublicStaticProperty(Names.EventSourceStatic);
				var messageTypeIdProperty = GetPublicStaticProperty(Names.MessageTypeIdStatic);
				var statsIdProperty = GetPublicStaticProperty(Names.StatsIdStatic);

				if (eventSourceProperty is null ||
					messageTypeIdProperty is null ||
					statsIdProperty is null) {

					Log.Warning($"{messageType} is not a registered stats message");
					return;
				}

				//qqq
				var eventSourceObject = eventSourceProperty.GetValue(null);
				if (eventSourceObject is null) {
					//qq
					return;
				}

				var candidate = eventSourceObject.GetType();
				Type closedGenericEventSourceType = null;
				while (candidate != null) {
					if (candidate.IsGenericType) {
						var openGenericType = candidate.GetGenericTypeDefinition();
						if (openGenericType == typeof(MyEventSource<>)) {
							closedGenericEventSourceType = candidate;
							break;
						}
					}

					candidate = candidate.BaseType;
				}

				if (closedGenericEventSourceType is null) {
					//qq
					return;
				}

				var enumType = closedGenericEventSourceType.GenericTypeArguments[0];

				var enumValueName = Enum.GetName(enumType, messageTypeIdProperty.GetValue(null));
				if (enumValueName is null) {
					//qq
					return;
				}

				//qq not 100% convinced the name should be the enum typename. Maybe it should be the name of the EventSource
				if (!configuration.TryGetValue(enumType.Name, out var groupConfiguration)) {
					//qq
					return;
				}

				if (!groupConfiguration.TryGetValue(enumValueName, out var statsIdOverride)) {
					//qq
					return;
				}

				//qqqqqqqqq well, gotta convert it to the enum value ofc.
				if (!Enum.TryParse(enumType, statsIdOverride, out var parsed)) {
					//qq
					return;
				}

				statsIdProperty.SetValue(null, parsed);

				Log.Information(
					"STAT-TASTIC! Overrode {enum}.{original} to {new} ",
					enumType.Name,
					enumValueName,
					statsIdOverride);
				//qq if we want we could track which ones settings we have consumed so that
				// we can log some warnings about unrecognised settings
			}
		}
	}
}
