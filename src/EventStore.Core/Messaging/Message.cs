using System;

namespace EventStore.Core.Messaging;

[AttributeUsage(AttributeTargets.Class)]
public class BaseMessageAttribute : Attribute {
	public BaseMessageAttribute() {
	}
}

[AttributeUsage(AttributeTargets.Class)]
public class DerivedMessageAttribute : Attribute {
	public DerivedMessageAttribute() {
	}

	public DerivedMessageAttribute(object messageGroup) {
	}
}

public interface Message {
	public int    MsgTypeId => -1;
	public string Label     => "";
}
	
public abstract class Message<T> : Message where T : Message {
	static Message() => MessageRegistrationInfo<T>.Initialize();

	public int    MsgTypeId => MessageRegistrationInfo<T>.TypeId;
	public string Label     => MessageRegistrationInfo<T>.Label;
}
