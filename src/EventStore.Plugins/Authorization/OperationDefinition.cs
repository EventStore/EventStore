using System;

namespace EventStore.Plugins.Authorization {
	public readonly struct OperationDefinition : IEquatable<OperationDefinition> {
		public string Resource { get; }
		public string Action { get; }

		public OperationDefinition(string resource, string action) {
			Resource = resource;
			Action = action;
		}

		public bool Equals(OperationDefinition other) {
			return Resource == other.Resource && Action == other.Action;
		}

		public override bool Equals(object obj) {
			return obj is OperationDefinition other && Equals(other);
		}

		public override int GetHashCode() {
			unchecked {
				return (Resource.GetHashCode() * 397) ^ Action.GetHashCode();
			}
		}

		public static bool operator ==(OperationDefinition left, OperationDefinition right) {
			return left.Equals(right);
		}

		public static bool operator !=(OperationDefinition left, OperationDefinition right) {
			return !left.Equals(right);
		}
	}
}
