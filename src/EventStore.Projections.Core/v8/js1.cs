using System;
using System.Runtime.InteropServices;

namespace EventStore.Projections.Core.v8 {
	public static class Js1 {
		private const string DllName = "js1";

		public delegate void CommandHandlerRegisteredDelegate(
			[MarshalAs(UnmanagedType.LPWStr)] string eventName, IntPtr handlerHandle);

		public delegate void ReverseCommandHandlerDelegate(
			[MarshalAs(UnmanagedType.LPWStr)] string commandName, [MarshalAs(UnmanagedType.LPWStr)] string commandBody);

		public delegate IntPtr LoadModuleDelegate(IntPtr prelude, [MarshalAs(UnmanagedType.LPWStr)] string moduleName);

		public delegate void LogDelegate([MarshalAs(UnmanagedType.LPWStr)] string message);

		public delegate void ReportErrorDelegate(int erroe_code,
			[MarshalAs(UnmanagedType.LPWStr)] string error_message);

		[return: MarshalAs(UnmanagedType.I1)]
		public delegate bool EnterCancellableRegionDelegate();

		[return: MarshalAs(UnmanagedType.I1)]
		public delegate bool ExitCancellableRegionDelegate();

		[DllImport("js1", EntryPoint = "js1_api_version")]
		public static extern IntPtr ApiVersion();

		[DllImport("js1", EntryPoint = "compile_module")]
		public static extern IntPtr CompileModule(
			IntPtr prelude, [MarshalAs(UnmanagedType.LPWStr)] string script,
			[MarshalAs(UnmanagedType.LPWStr)] string fileName);

		[DllImport("js1", EntryPoint = "compile_prelude")]
		public static extern IntPtr CompilePrelude(
			[MarshalAs(UnmanagedType.LPWStr)] string prelude, [MarshalAs(UnmanagedType.LPWStr)] string fileName,
			LoadModuleDelegate loadModuleHandler, EnterCancellableRegionDelegate enterCancellableRegionHandler,
			ExitCancellableRegionDelegate exitCancellableRegionHandler, LogDelegate logHandler);

		[DllImport("js1", EntryPoint = "compile_query")]
		public static extern IntPtr CompileQuery(
			IntPtr prelude, [MarshalAs(UnmanagedType.LPWStr)] string script,
			[MarshalAs(UnmanagedType.LPWStr)] string fileName,
			CommandHandlerRegisteredDelegate commandHandlerRegisteredCallback,
			ReverseCommandHandlerDelegate reverseCommandHandler);

		[DllImport("js1", EntryPoint = "dispose_script")]
		public static extern void DisposeScript(IntPtr scriptHandle);

		//TODO: add no result execute_handler
		[DllImport("js1", EntryPoint = "execute_command_handler")]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ExecuteCommandHandler(
			IntPtr scriptHandle, IntPtr eventHandlerHandle, [MarshalAs(UnmanagedType.LPWStr)] string dataJson,
			[MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)]
			string[] dataOther, int otherLength,
			out IntPtr resultJson, out IntPtr result2Json, out IntPtr memoryHandle);

		[DllImport("js1", EntryPoint = "free_result")]
		public static extern void FreeResult(IntPtr resultHandle);

		[DllImport("js1", EntryPoint = "terminate_execution")]
		public static extern void TerminateExecution(IntPtr scriptHandle);

		[DllImport("js1", EntryPoint = "report_errors")]
		public static extern void ReportErrors(IntPtr scriptHandle, ReportErrorDelegate reportErrorCallback);
	}
}
