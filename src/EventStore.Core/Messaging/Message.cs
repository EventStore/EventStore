using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ILogger = Serilog.ILogger;

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

public abstract class Message {
	public virtual int    MsgTypeId => -1;
	public virtual string Label     => "";
}
	
public abstract class Message<T> : Message where T : Message {
	static Message() => MessageRegistrationInfo<T>.Initialize();

	public override int    MsgTypeId => MessageRegistrationInfo<T>.TypeId;
	public override string Label     => MessageRegistrationInfo<T>.Label;
}
