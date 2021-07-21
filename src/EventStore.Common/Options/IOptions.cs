namespace EventStore.Common.Options {
	public interface IOptions {
		bool Help { get; }
		bool Version { get; }
		string Config { get; }
		string Log { get; }
		string LogConfig { get; }
		bool WhatIf { get; }
	}
}
