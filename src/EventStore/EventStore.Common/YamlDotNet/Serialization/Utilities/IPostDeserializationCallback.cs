
namespace YamlDotNet.Serialization.Utilities
{
	/// <summary>
	/// Indicates that a class used as deserialization state
	/// needs to be notified after deserialization.
	/// </summary>
	public interface IPostDeserializationCallback
	{
		void OnDeserialization();
	}
}
