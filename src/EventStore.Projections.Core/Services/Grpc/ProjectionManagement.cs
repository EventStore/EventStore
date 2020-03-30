using System;
using System.Linq;
using System.Text.Json;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Plugins.Authorization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace EventStore.Client.Projections {
	partial class Projections {
		partial class ProjectionsBase : ServiceBase {
		}
	}
}

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement : EventStore.Client.Projections.Projections.ProjectionsBase {
		private readonly IQueuedHandler _queue;
		private readonly IAuthorizationProvider _authorizationProvider;

		public ProjectionManagement(IQueuedHandler queue, IAuthorizationProvider authorizationProvider) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			if (authorizationProvider == null) throw new ArgumentNullException(nameof(authorizationProvider));
			_queue = queue;
			_authorizationProvider = authorizationProvider;
		}

		private static Exception UnknownMessage<T>(Message message) where T : Message =>
			new RpcException(
				new Status(StatusCode.Unknown,
					$"Envelope callback expected {typeof(T).Name}, received {message.GetType().Name} instead"));

		private static Value GetProtoValue(JsonElement element) =>
			element.ValueKind switch {
				JsonValueKind.Null => new Value {NullValue = NullValue.NullValue},
				JsonValueKind.Array => new Value {
					ListValue = new ListValue {
						Values = {
							element.EnumerateArray().Select(GetProtoValue)
						}
					}
				},
				JsonValueKind.False => new Value {BoolValue = false},
				JsonValueKind.True => new Value {BoolValue = true},
				JsonValueKind.String => new Value {StringValue = element.GetString()},
				JsonValueKind.Number => new Value {NumberValue = element.GetDouble()},
				JsonValueKind.Object => new Value {StructValue = GetProtoStruct(element)},
				JsonValueKind.Undefined => new Value(),
				_ => throw new InvalidOperationException()
			};

		private static Struct GetProtoStruct(JsonElement element) {
			var structValue = new Struct();
			foreach (var property in element.EnumerateObject()) {
				structValue.Fields.Add(property.Name, GetProtoValue(property.Value));
			}

			return structValue;
		}
	}
}
