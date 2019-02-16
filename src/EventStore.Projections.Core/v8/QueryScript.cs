using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EventStore.Common.Utils;
using EventStore.Projections.Core.Messages;
using EventStore.Common.Log;

namespace EventStore.Projections.Core.v8 {
	public class QueryScript : IDisposable {
		private readonly ILogger Log = LogManager.GetLoggerFor<QueryScript>();
		private readonly PreludeScript _prelude;
		private readonly CompiledScript _script;
		private readonly Dictionary<string, IntPtr> _registeredHandlers = new Dictionary<string, IntPtr>();

		private Func<string, string[], string> _getStatePartition;
		private Func<string, string[], string> _transformCatalogEvent;
		private Func<string, string[], Tuple<string, string>> _processEvent;
		private Func<string, string[], string> _processDeletedNotification;
		private Func<string, string[], string> _processCreatedNotification;
		private Func<string> _transformStateToResult;
		private Action<string> _setState;
		private Action<string> _setSharedState;
		private Action _initialize;
		private Action _initialize_shared;
		private Func<string> _getSources;

		// the following two delegates must be kept alive while used by unmanaged code
		private readonly Js1.CommandHandlerRegisteredDelegate _commandHandlerRegisteredCallback; // do not inline
		private readonly Js1.ReverseCommandHandlerDelegate _reverseCommandHandlerDelegate; // do not inline
		private QuerySourcesDefinition _sources;
		private Exception _reverseCommandHandlerException;

		public event Action<string> Emit;

		public QueryScript(PreludeScript prelude, string script, string fileName) {
			_prelude = prelude;
			_commandHandlerRegisteredCallback = CommandHandlerRegisteredCallback;
			_reverseCommandHandlerDelegate = ReverseCommandHandler;

			_script = CompileScript(prelude, script, fileName);

			try {
				GetSources();
			} catch {
				Dispose();
				throw;
			}
		}

		private CompiledScript CompileScript(PreludeScript prelude, string script, string fileName) {
			prelude.ScheduleTerminateExecution();
			IntPtr query = Js1.CompileQuery(
				prelude.GetHandle(), script, fileName, _commandHandlerRegisteredCallback,
				_reverseCommandHandlerDelegate);
			var terminated = prelude.CancelTerminateExecution();
			CompiledScript.CheckResult(query, terminated, disposeScriptOnException: true);
			return new CompiledScript(query);
		}

		private void ReverseCommandHandler(string commandName, string commandBody) {
			try {
				switch (commandName) {
					case "emit":
						DoEmit(commandBody);
						break;
					default:
						Log.Debug("Ignoring unknown reverse command: '{command}'", commandName);
						break;
				}
			} catch (Exception ex) {
				// report only the first exception occured in reverse command handler
				if (_reverseCommandHandlerException == null)
					_reverseCommandHandlerException = ex;
			}
		}

		private void CommandHandlerRegisteredCallback(string commandName, IntPtr handlerHandle) {
			_registeredHandlers.Add(commandName, handlerHandle);
			//TODO: change to dictionary
			switch (commandName) {
				case "initialize":
					_initialize = () => ExecuteHandler(handlerHandle, "");
					break;
				case "initialize_shared":
					_initialize_shared = () => ExecuteHandler(handlerHandle, "");
					break;
				case "get_state_partition":
					_getStatePartition = (json, other) => ExecuteHandler(handlerHandle, json, other);
					break;
				case "process_event":
					string newSharedState;
					_processEvent =
						(json, other) =>
							Tuple.Create(ExecuteHandler(handlerHandle, json, other, out newSharedState),
								newSharedState);
					break;
				case "process_deleted_notification":
					_processDeletedNotification = (json, other) => ExecuteHandler(handlerHandle, json, other);
					break;
				case "process_created_notification":
					_processCreatedNotification = (json, other) => ExecuteHandler(handlerHandle, json, other);
					break;
				case "transform_catalog_event":
					_transformCatalogEvent = (json, other) => ExecuteHandler(handlerHandle, json, other);
					break;
				case "transform_state_to_result":
					_transformStateToResult = () => ExecuteHandler(handlerHandle, "");
					break;
				case "test_array":
					break;
				case "set_state":
					_setState = json => ExecuteHandler(handlerHandle, json);
					break;
				case "set_shared_state":
					_setSharedState = json => ExecuteHandler(handlerHandle, json);
					break;
				case "get_sources":
					_getSources = () => ExecuteHandler(handlerHandle, "");
					break;
				case "set_debugging":
				case "debugging_get_state":
					// ignore - browser based debugging only
					break;
				default:
					Log.Debug("Unknown command handler registered. Command name: {command}", commandName);
					break;
			}
		}

		private void DoEmit(string commandBody) {
			OnEmit(commandBody);
		}

		private void GetSources() {
			if (_getSources == null)
				throw new InvalidOperationException("'get_sources' command handler has not been registered");
			var sourcesJson = _getSources();


			_sources = sourcesJson.ParseJson<QuerySourcesDefinition>();
		}

		private string ExecuteHandler(
			IntPtr commandHandlerHandle, string json, string[] other = null) {
			string newSharedState;
			return ExecuteHandler(commandHandlerHandle, json, other, out newSharedState);
		}

		private string ExecuteHandler(
			IntPtr commandHandlerHandle, string json, string[] other, out string newSharedState) {
			_reverseCommandHandlerException = null;

			_prelude.ScheduleTerminateExecution();

			IntPtr resultJsonPtr;
			IntPtr result2JsonPtr;
			IntPtr memoryHandle;
			bool success = Js1.ExecuteCommandHandler(
				_script.GetHandle(), commandHandlerHandle, json, other, other != null ? other.Length : 0,
				out resultJsonPtr, out result2JsonPtr, out memoryHandle);

			var terminated = _prelude.CancelTerminateExecution();
			if (!success)
				CompiledScript.CheckResult(_script.GetHandle(), terminated, disposeScriptOnException: false);
			string resultJson = Marshal.PtrToStringUni(resultJsonPtr);
			string result2Json = Marshal.PtrToStringUni(result2JsonPtr);
			Js1.FreeResult(memoryHandle);
			if (_reverseCommandHandlerException != null) {
				throw new ApplicationException(
					"An exception occurred while executing a reverse command handler. "
					+ _reverseCommandHandlerException.Message, _reverseCommandHandlerException);
			}

			newSharedState = result2Json;
			return resultJson;
		}

		private void OnEmit(string obj) {
			Action<string> handler = Emit;
			if (handler != null) handler(obj);
		}

		public void Dispose() {
			_script.Dispose();
		}

		public void Initialize() {
			InitializeScript();
		}

		public void InitializeShared() {
			InitializeScriptShared();
		}

		private void InitializeScript() {
			if (_initialize != null)
				_initialize();
		}

		private void InitializeScriptShared() {
			if (_initialize_shared != null)
				_initialize_shared();
		}

		public string GetPartition(string json, string[] other) {
			if (_getStatePartition == null)
				throw new InvalidOperationException("'get_state_partition' command handler has not been registered");

			return _getStatePartition(json, other);
		}

		public string TransformCatalogEvent(string json, string[] other) {
			if (_transformCatalogEvent == null)
				throw new InvalidOperationException(
					"'transform_catalog_event' command handler has not been registered");

			return _transformCatalogEvent(json, other);
		}

		public Tuple<string, string> Push(string json, string[] other) {
			if (_processEvent == null)
				throw new InvalidOperationException("'process_event' command handler has not been registered");

			return _processEvent(json, other);
		}

		public string NotifyDeleted(string json, string[] other) {
			if (_processDeletedNotification == null)
				throw new InvalidOperationException(
					"'process_deleted_notification' command handler has not been registered");

			return _processDeletedNotification(json, other);
		}

		public string NotifyCreated(string json, string[] other) {
			if (_processCreatedNotification == null)
				throw new InvalidOperationException(
					"'process_created_notification' command handler has not been registered");

			return _processCreatedNotification(json, other);
		}

		public string TransformStateToResult() {
			if (_transformStateToResult == null)
				throw new InvalidOperationException(
					"'transform_state_to_result' command handler has not been registered");

			return _transformStateToResult();
		}

		public void SetState(string state) {
			if (_setState == null)
				throw new InvalidOperationException("'set_state' command handler has not been registered");
			_setState(state);
		}

		public void SetSharedState(string state) {
			if (_setSharedState == null)
				throw new InvalidOperationException("'set_shared_state' command handler has not been registered");
			_setSharedState(state);
		}

		public QuerySourcesDefinition GetSourcesDefintion() {
			return _sources;
		}
	}
}
