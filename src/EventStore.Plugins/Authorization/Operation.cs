using System;
using System.Text;

namespace EventStore.Plugins.Authorization {
	public readonly struct Operation {
		public string Resource { get; }
		public string Action { get; }
		public ReadOnlyMemory<Parameter> Parameters { get; }

		public Operation(OperationDefinition definition) : this(definition.Resource, definition.Action) {
		}

		public Operation(string resource, string action) : this(resource, action, Array.Empty<Parameter>()) { }

		public Operation WithParameter(string name, string value) {
			return WithParameters(new Parameter(name, value));
		}

		public Operation WithParameter(Parameter parameter) {
			return WithParameters(parameter);
		}

		public Operation WithParameters(ReadOnlyMemory<Parameter> parameters) {
			var memory = new Memory<Parameter>(new Parameter[Parameters.Length + parameters.Length]);
			if (!Parameters.IsEmpty) Parameters.CopyTo(memory);
			parameters.CopyTo(memory.Slice(Parameters.Length));
			return new Operation(Resource, Action, memory);
		}

		public Operation WithParameters(params Parameter[] parameters) {
			return WithParameters(new ReadOnlyMemory<Parameter>(parameters));
		}

		public Operation(string resource, string action, Memory<Parameter> parameters) {
			Resource = resource;
			Action = action;
			Parameters = parameters;
		}

		public static implicit operator OperationDefinition(Operation operation) {
			return new OperationDefinition(operation.Resource, operation.Action);
		}

		public override string ToString() {
			var sb = new StringBuilder();
			sb.Append($"{Resource} : {Action}");
			var parameters = Parameters.Span;
			if (!parameters.IsEmpty) {
				sb.Append(" p: {");
				while (!parameters.IsEmpty) {
					sb.Append($"{parameters[0].Name} : {parameters[0].Value}");
					parameters = parameters.Slice(1);
				}

				sb.Append("}");
			}

			return sb.ToString();
		}
	}
}
