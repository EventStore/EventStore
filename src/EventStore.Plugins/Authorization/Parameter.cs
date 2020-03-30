using System;

namespace EventStore.Plugins.Authorization {
	public readonly struct Parameter : IEquatable<Parameter> {
		public Parameter(string name, string value) {
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Value = value ?? throw new ArgumentNullException(nameof(value));
		}

		public string Name { get; }
		public string Value { get; }

		public bool Equals(Parameter other) {
			return Name == other.Name && Value == other.Value;
		}

		public override bool Equals(object obj) {
			return obj is Parameter other && Equals(other);
		}

		public override int GetHashCode() {
			unchecked {
				return (Name.GetHashCode() * 397) ^ Value.GetHashCode();
			}
		}

		public static bool operator ==(Parameter left, Parameter right) {
			return left.Equals(right);
		}

		public static bool operator !=(Parameter left, Parameter right) {
			return !left.Equals(right);
		}

		public override string ToString() {
			return $"{Name} : {Value}";
		}
	}
}
