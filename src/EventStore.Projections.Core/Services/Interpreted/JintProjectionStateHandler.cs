using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using ILogger = Serilog.ILogger;


#nullable enable
namespace EventStore.Projections.Core.Services.Interpreted {
	public class JintProjectionStateHandler : IProjectionStateHandler {
		private readonly bool _enableContentTypeValidation;
		static readonly Stopwatch _sw = Stopwatch.StartNew();
		private readonly Engine _engine;
		private readonly SourceDefinitionBuilder _definitionBuilder;
		private readonly List<EmittedEventEnvelope> _emitted;
		private readonly InterpreterRuntime _interpreterRuntime;
		private readonly JsonInstance _json;
		private CheckpointTag? _currentPosition;

		private JsValue _state;
		private JsValue _sharedState;
		
		public JintProjectionStateHandler(string source, bool enableContentTypeValidation, TimeSpan compilationTimeout, TimeSpan executionTimeout) {

			_enableContentTypeValidation = enableContentTypeValidation;
			_definitionBuilder = new SourceDefinitionBuilder();
			_definitionBuilder.NoWhen();
			_definitionBuilder.AllEvents();
			TimeConstraint timeConstraint = new(compilationTimeout, executionTimeout);
			_engine = new Engine(opts => opts.Constraint(timeConstraint));
			_engine.Global.RemoveOwnProperty("eval");
			_state = JsValue.Undefined;
			_sharedState = JsValue.Undefined;
			_interpreterRuntime = new InterpreterRuntime(_engine, _definitionBuilder);



			timeConstraint.Compiling();
			_engine.Execute(source);
			timeConstraint.Executing();
			_json = _interpreterRuntime.SwitchToExecutionMode();
			_engine.Global.FastAddProperty("emit", new ClrFunctionInstance(_engine, "emit", Emit, 4), true, false, true);
			_engine.Global.FastAddProperty("linkTo", new ClrFunctionInstance(_engine, "linkTo", LinkTo, 3), true, false, true);
			_engine.Global.FastAddProperty("linkStreamTo", new ClrFunctionInstance(_engine, "linkStreamTo", LinkStreamTo, 3), true, false, true);
			_engine.Global.FastAddProperty("copyTo", new ClrFunctionInstance(_engine, "copyTo", CopyTo, 3), true, false, true);
			_emitted = new List<EmittedEventEnvelope>();
		}



		public void Dispose() {
		}

		public IQuerySources GetSourceDefinition() {
			_engine.ResetConstraints();
			return _definitionBuilder.Build();
		}

		public void Load(string? state) {
			_engine.ResetConstraints();
			if (state != null) {
				var jsValue = _json.Parse(_interpreterRuntime, new JsValue[] { new JsString(state) });
				LoadCurrentState(jsValue);
			} else {
				LoadCurrentState(JsValue.Null);
			}
		}

		private void LoadCurrentState(JsValue jsValue) {
			if (_definitionBuilder.IsBiState) {
				if (_state == null || _state == JsValue.Undefined)
					_state = new ArrayInstance(_engine, new[]
					{
						PropertyDescriptor.Undefined, PropertyDescriptor.Undefined
					});

				_state.AsArray().Set(0, jsValue);
			} else {
				_state = jsValue;
			}
		}

		public void LoadShared(string? state) {
			_engine.ResetConstraints();
			if (state != null) {
				var jsValue = _json.Parse(_interpreterRuntime, new JsValue[] {new JsString(state)});
				LoadCurrentSharedState(jsValue);
			} else {
				LoadCurrentSharedState(JsValue.Null);
			}

			
		}

		private void LoadCurrentSharedState(JsValue jsValue) {
			if (_definitionBuilder.IsBiState) {
				if (_state == null || _state == JsValue.Undefined)
					_state = new ArrayInstance(_engine, new[]
					{
						PropertyDescriptor.Undefined, PropertyDescriptor.Undefined
					});

				_state.AsArray().Set(1, jsValue);
			} else {
				_state = jsValue;
			}
		}

		public void Initialize() {
			_engine.ResetConstraints();
			var state = _interpreterRuntime.InitializeState();
			LoadCurrentState(state);

		}

		public void InitializeShared() {
			_engine.ResetConstraints();
			_sharedState = _interpreterRuntime.InitializeSharedState();
			LoadCurrentSharedState(_sharedState);
		}

		public string? GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data) {
			_currentPosition = eventPosition;
			_engine.ResetConstraints();
			var envelope = _interpreterRuntime.CreateEnvelope("", data, category);
			var partition = _interpreterRuntime.GetPartition(envelope);
			if (partition == JsValue.Null || partition == JsValue.Undefined || !partition.IsString())
				return null;
			return partition.AsString();
		}

		public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent @event,
			out EmittedEventEnvelope[]? emittedEvents) {
			_engine.ResetConstraints();
			_currentPosition = createPosition;
			var envelope = _interpreterRuntime.CreateEnvelope(partition, @event, "");
			_interpreterRuntime.HandleCreated(_state, envelope);

			emittedEvents = _emitted.Count > 0 ? _emitted.ToArray() : null;
			_emitted.Clear();
			return true;
		}

		public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string? newState) {
			_engine.ResetConstraints();
			_currentPosition = deletePosition;

			_interpreterRuntime.HandleDeleted(partition, false);
			newState = ConvertToStringHandlingNulls(_json, _state);
			return true;
		}

		public string? TransformStateToResult() {
			_engine.ResetConstraints();
			var result = _interpreterRuntime.TransformStateToResult(_state);
			if (result == JsValue.Null || result == JsValue.Undefined)
				return null;
			return _json.Stringify(JsValue.Null, new[] { result }).AsString();
		}

		public bool ProcessEvent(string partition, CheckpointTag eventPosition, string category, ResolvedEvent @event,
			out string? newState, out string? newSharedState, out EmittedEventEnvelope[]? emittedEvents) {
			_currentPosition = eventPosition;
			_engine.ResetConstraints();
			if ((@event.IsJson && string.IsNullOrWhiteSpace(@event.Data)) ||
				(!_enableContentTypeValidation && !@event.IsJson && string.IsNullOrEmpty(@event.Data))) {
				PrepareOutput(out newState, out newSharedState, out emittedEvents);
				return true;
			}
			
			var envelope = _interpreterRuntime.CreateEnvelope(partition, @event, category);
			_state = _interpreterRuntime.Handle(_state, envelope);
			PrepareOutput(out newState, out newSharedState, out emittedEvents);
			return true;
		}

		private void PrepareOutput(out string? newState, out string? newSharedState, out EmittedEventEnvelope[]? emittedEvents) {
			emittedEvents = _emitted.Count > 0 ? _emitted.ToArray() : null;
			_emitted.Clear();
			if (_definitionBuilder.IsBiState && _state.IsArray()) {
				var arr = _state.AsArray();
				if (arr.TryGetValue(0, out var state)) {
					if (_state.IsString()) {
						newState = _state.AsString();
					} else {
						newState = ConvertToStringHandlingNulls(_json, state);
					}
				} else {
					newState = "";
				}

				if (arr.TryGetValue(1, out var sharedState)) {
					newSharedState = ConvertToStringHandlingNulls(_json, sharedState);
				} else {
					newSharedState = null;
				}

			}else if (_state.IsString()) {
				newState = _state.AsString();
				newSharedState = null;
			}
			else {
				newState = ConvertToStringHandlingNulls(_json, _state);
				newSharedState = null;
			}
			

			
		}

		private static string? ConvertToStringHandlingNulls(JsonInstance json, JsValue value) {
			if (value.IsNull() || value.IsUndefined())
				return null;
			return json.Stringify(JsValue.Undefined, new[] { value }).AsString();
		}
		
		JsValue Emit(JsValue thisValue, JsValue[] parameters) {
			if (parameters.Length < 3)
				throw new ArgumentException("invalid number of parameters");

			string stream = EnsureNonNullStringValue(parameters.At(0), "streamId");
			var eventType = EnsureNonNullStringValue(parameters.At(1), "eventName");
			var eventBody = EnsureNonNullObjectValue(parameters.At(2), "eventBody");

			if (parameters.Length == 4 && !parameters.At(3).IsObject())
#pragma warning disable CA2208 // ReSharper disable once NotResolvedInText 
				throw new ArgumentException("object expected", "metadata");
#pragma warning restore CA2208 

			var data = _json.Stringify(JsValue.Undefined, new JsValue[] { eventBody }).AsString();
			ExtraMetaData? metadata = null;
			if (parameters.Length == 4) {
				var md = parameters.At(3).AsObject();
				var d = new Dictionary<string, string?>();
				foreach (var kvp in md.GetOwnProperties()) {
					d.Add(kvp.Key.AsString(), AsString(kvp.Value.Value));
				}

				metadata = new ExtraMetaData(d);
			}
			_emitted.Add(new EmittedEventEnvelope(new EmittedDataEvent(stream, Guid.NewGuid(), eventType, true, data, metadata, _currentPosition, null)));
			return JsValue.Undefined;
		}

		private static ObjectInstance EnsureNonNullObjectValue(JsValue parameter, string parameterName) {
			if (parameter == JsValue.Null || parameter == JsValue.Undefined)
				throw new ArgumentNullException(parameterName);
			if (!parameter.IsObject())
				throw new ArgumentException("object expected", parameterName);
			return parameter.AsObject();
		}

		private static string EnsureNonNullStringValue(JsValue parameter, string parameterName) {
			if (parameter != JsValue.Null &&
				parameter.IsString() &&
				(parameter.AsString() is { } value &&
				 !string.IsNullOrWhiteSpace(value)))
				return value;

			if (parameter == JsValue.Null || parameter == JsValue.Undefined || parameter.IsString())
				throw new ArgumentNullException(parameterName);

			throw new ArgumentException("string expected", parameterName);
		}

		string? AsString(JsValue? value) {
			return value switch {
				JsBoolean b => b.AsBoolean() ? "true" : "false",
				JsString s => s.AsString(),
				JsNumber n => n.AsNumber().ToString(CultureInfo.InvariantCulture),
				JsNull => null,
				JsUndefined => null,
				{ } v => _json.Stringify(JsValue.Undefined, new[] { v }).AsString(),
				_ => null
			};
		}

		JsValue LinkTo(JsValue thisValue, JsValue[] parameters) {
			if (parameters.Length != 2 && parameters.Length != 3)
				throw new ArgumentException("wrong number of parameters");
			var stream = EnsureNonNullStringValue(parameters.At(0), "streamId");
			var @event = EnsureNonNullObjectValue(parameters.At(1), "event");
			
			if (!@event.TryGetValue("sequenceNumber", out var numberValue) | !@event.TryGetValue("streamId", out var sourceValue) || !numberValue.IsNumber()
			     || !sourceValue.IsString()) {
				throw new Exception($"Invalid link to event {numberValue}@{sourceValue}");
			}

			var number = (long)numberValue.AsNumber();
			var source = sourceValue.AsString();
			ExtraMetaData? metadata = null;
			if (parameters.Length == 3) {
				var md = parameters.At(4).AsObject();
				var d = new Dictionary<string, string?>();
				foreach (var kvp in md.GetOwnProperties()) {
					d.Add(kvp.Key.AsString(), AsString(kvp.Value.Value));
				}
				metadata = new ExtraMetaData(d);
			}

			_emitted.Add(new EmittedEventEnvelope(
				new EmittedDataEvent(stream, Guid.NewGuid(), SystemEventTypes.LinkTo, false, $"{number}@{source}", metadata, _currentPosition, null)));
			return JsValue.Undefined;
		}

		JsValue LinkStreamTo(JsValue thisValue, JsValue[] parameters) {

			var stream = EnsureNonNullStringValue(parameters.At(0), "streamId");
			var linkedStreamId = EnsureNonNullStringValue(parameters.At(1), "linkedStreamId");
			if (parameters.Length == 3) {

			}

			ExtraMetaData? metadata = null;
			if (parameters.Length == 3) {
				var md = parameters.At(4).AsObject();
				var d = new Dictionary<string, string?>();
				foreach (var kvp in md.GetOwnProperties()) {
					d.Add(kvp.Key.AsString(), AsString(kvp.Value.Value));
				}
				metadata = new ExtraMetaData(d);
			}
			_emitted.Add(new EmittedEventEnvelope(
				new EmittedDataEvent(stream, Guid.NewGuid(), SystemEventTypes.StreamReference, false, linkedStreamId, metadata, _currentPosition, null)));
			return JsValue.Undefined;
		}

		JsValue CopyTo(JsValue thisValue, JsValue[] parameters) {
			return JsValue.Undefined;
		}

		class TimeConstraint : IConstraint {
			private readonly TimeSpan _compilationTimeout;
			private readonly TimeSpan _executionTimeout;
			private TimeSpan _start;
			private TimeSpan _timeout;
			
			public TimeConstraint(TimeSpan compilationTimeout, TimeSpan executionTimeout) {
				_compilationTimeout = compilationTimeout;
				_executionTimeout = executionTimeout;
				_timeout = _compilationTimeout;
			}

			public void Compiling() {
				_timeout = _compilationTimeout;
			}

			public void Executing() {

				_timeout = _executionTimeout;

			}
			public void Reset() {
				_start = _sw.Elapsed;
			}

			public void Check() {
				if (_sw.Elapsed - _start >= _timeout) {
					if (Debugger.IsAttached)
						return;
					throw new TimeoutException($"Projection script took too long to execute (took: {_sw.Elapsed - _start:c}, allowed: {_timeout:c}");
				}
			}
		}

		class InterpreterRuntime : ObjectInstance {

			private readonly Dictionary<string, ScriptFunctionInstance> _handlers;
			private readonly List<(TransformType, ScriptFunctionInstance)> _transforms;
			private readonly List<ScriptFunctionInstance> _createdHandlers;
			private ScriptFunctionInstance? _init;
			private ScriptFunctionInstance? _initShared;
			private ScriptFunctionInstance? _any;
			private ScriptFunctionInstance? _deleted;
			private ScriptFunctionInstance? _partitionFunction;

			private readonly JsValue _whenInstance;
			private readonly JsValue _partitionByInstance;
			private readonly JsValue _outputStateInstance;
			private readonly JsValue _foreachStreamInstance;
			private readonly JsValue _transformByInstance;
			private readonly JsValue _filterByInstance;
			private readonly JsValue _outputToInstance;
			private readonly JsValue _definesStateTransformInstance;

			private readonly SourceDefinitionBuilder _definitionBuilder;
			private readonly JsonInstance _json;

			private readonly ILogger _logger;

			private static readonly IReadOnlyDictionary<string, Action<InterpreterRuntime>> _possibleProperties = new Dictionary<string, Action<InterpreterRuntime>>() {
				["when"] = i => i.FastAddProperty("when", i._whenInstance, true, false, true),
				["partitionBy"] = i => i.FastAddProperty("partitionBy", i._partitionByInstance, true, false, true),
				["outputState"] = i => i.FastAddProperty("outputState", i._outputStateInstance, true, false, true),
				["foreachStream"] = i => i.FastAddProperty("foreachStream", i._foreachStreamInstance, true, false, true),
				["transformBy"] = i => i.FastAddProperty("transformBy", i._transformByInstance, true, false, true),
				["filterBy"] = i => i.FastAddProperty("filterBy", i._filterByInstance, true, false, true),
				["outputTo"] = i => i.FastAddProperty("outputTo", i._outputToInstance, true, false, true),
				["$defines_state_transform"] = i => i.FastAddProperty("$defines_state_transform", i._definesStateTransformInstance, true, false, true),
			};

			private static readonly IReadOnlyDictionary<string, string[]> _availableProperties = new Dictionary<string, string[]>() {
				["fromStream"] = new[] { "when", "partitionBy", "outputState" },
				["fromAll"] = new[] { "when", "partitionBy", "outputState", "foreachStream" },
				["fromStreams"] = new[] { "when", "partitionBy", "outputState" },
				["fromCategory"] = new[] { "when", "partitionBy", "outputState", "foreachStream" },
				["when"] = new[] { "transformBy", "filterBy", "outputState", "outputTo", "$defines_state_transform" },
				["foreachStream"] = new[] { "when" },
				["outputState"] = new[] { "transformBy", "filterBy", "outputTo" },
				["partitionBy"] = new[] { "when" },
				["transformBy"] = new[] { "transformBy", "filterBy", "outputState", "outputTo" },
				["filterBy"] = new[] { "transformBy", "filterBy", "outputState", "outputTo" },
				["outputTo"] = Array.Empty<string>(),
				["execution"] = Array.Empty<string>()
			};

			private static readonly IReadOnlyDictionary<string, Action<SourceDefinitionBuilder, JsValue>> _setters =
				new Dictionary<string, Action<SourceDefinitionBuilder, JsValue>>(StringComparer.OrdinalIgnoreCase) {
					{"$includeLinks", (options, value) => options.SetIncludeLinks(value.IsBoolean()? value.AsBoolean() : throw new Exception("Invalid value"))},
					{"reorderEvents", (options, value) => options.SetReorderEvents(value.IsBoolean()? value.AsBoolean(): throw new Exception("Invalid value"))},
					{"processingLag", (options, value) => options.SetProcessingLag(value.IsNumber() ? (int)value.AsNumber() : throw new Exception("Invalid value"))},
					{"resultStreamName", (options, value) => options.SetResultStreamNameOption(value.IsString() ? value.AsString() : throw new Exception("Invalid value"))},
					{"biState", (options, value) => options.SetIsBiState(value.IsBoolean()? value.AsBoolean() : throw new Exception("Invalid value"))},
				};

			private readonly List<string> _definitionFunctions;
			private readonly EventEnvelope _envelope;


			public InterpreterRuntime(Engine engine, SourceDefinitionBuilder builder) : base(engine) {
				_logger = Serilog.Log.ForContext<InterpreterRuntime>();
				_definitionBuilder = builder;
				_handlers = new Dictionary<string, ScriptFunctionInstance>(StringComparer.Ordinal);
				_createdHandlers = new List<ScriptFunctionInstance>();
				_transforms = new List<(TransformType, ScriptFunctionInstance)>();
				_json = JsonInstance.CreateJsonObject(_engine);
				_definitionFunctions = new List<string>();
				_envelope = new EventEnvelope(_engine, _json);
				_engine.Global.FastAddProperty("log", new ClrFunctionInstance(_engine, "log", Log), false, false, false);
				AddDefinitionFunction("options", SetOptions, 1);
				AddDefinitionFunction("fromStream", FromStream, 1);
				AddDefinitionFunction("fromCategory", FromCategory, 4);
				AddDefinitionFunction("fromCategories", FromCategory, 4);
				AddDefinitionFunction("fromAll", FromAll, 0);
				AddDefinitionFunction("fromStreams", FromStreams, 1);
				AddDefinitionFunction("on_event", OnEvent, 1);
				AddDefinitionFunction("on_any", OnAny, 1);
				_whenInstance = new ClrFunctionInstance(engine, "when", When, 1);
				_partitionByInstance = new ClrFunctionInstance(engine, "partitionBy", PartitionBy, 1);
				_outputStateInstance = new ClrFunctionInstance(engine, "outputState", OutputState, 1);
				_foreachStreamInstance = new ClrFunctionInstance(engine, "foreachStream", ForEachStream, 1);
				_transformByInstance = new ClrFunctionInstance(engine, "transformBy", TransformBy, 1);
				_filterByInstance = new ClrFunctionInstance(engine, "filterBy", FilterBy, 1);
				_outputToInstance = new ClrFunctionInstance(engine, "outputTo", OutputTo, 1);
				_definesStateTransformInstance = new ClrFunctionInstance(engine, "$defines_state_transform", DefinesStateTransform);

			}

			private void AddDefinitionFunction(string name, Func<JsValue, JsValue[], JsValue> func, int length) {
				_definitionFunctions.Add(name);
				_engine.Global.FastAddProperty(name, new ClrFunctionInstance(_engine, name, func, length), true, false, true);
			}

			void Log(string message) {
				_logger.Debug(message, Array.Empty<object>());
			}

			private JsValue Log(JsValue thisValue, JsValue[] parameters) {
				if (parameters.Length == 0)
					return Undefined;
				if (parameters.Length == 1) {
					var p0 = parameters.At(0);
					if (p0 != null && p0.IsPrimitive())
						Log(p0.ToString());
					if (p0 is ObjectInstance oi)
						Log(_json.Stringify(Undefined, new JsValue[] { oi }).AsString());
					return Undefined;
				}

				
				if (parameters.Length > 1) {
					var sb = new StringBuilder();
					for (int i = 0; i < parameters.Length; i++) {
						if (i > 1) sb.Append(" ,");
						var p = parameters.At(i);
						if (p != null && p.IsPrimitive())
							Log(p.ToString());
						if (p is ObjectInstance oi)
							sb.Append(_json.Stringify(Undefined, new JsValue[] {oi}).AsString());
					}

					Log(sb.ToString());
				}
				return Undefined;
			}

			private JsValue FromStream(JsValue _, JsValue[] parameters) {
				var stream = parameters.At(0);
				if (stream is not JsString)
					throw new ArgumentException("stream");
				_definitionBuilder.FromStream(stream.AsString());
				RestrictProperties("fromStream");

				return this;
			}

			private JsValue FromCategory(JsValue thisValue, JsValue[] parameters) {
				if (parameters.Length == 0)
					return this;
				if (parameters.Length == 1 && parameters.At(0).IsArray()) {
					foreach (var cat in parameters.At(0).AsArray()) {
						if (cat is not JsString s) {
							throw new ArgumentException("categories");
						}
						_definitionBuilder.FromStream($"$ce-{s.AsString()}");
					}
				} else if (parameters.Length > 1) {
					foreach (var cat in parameters) {
						if (cat is not JsString s) {
							throw new ArgumentException("categories");
						}
						_definitionBuilder.FromStream($"$ce-{s.AsString()}");
					}
				} else {
					var p0 = parameters.At(0);
					if (p0 is not JsString s)
						throw new ArgumentException("category");
					_definitionBuilder.FromCategory(s.AsString());
				}

				RestrictProperties("fromCategory");

				return this;
			}

			private JsValue When(JsValue thisValue, JsValue[] parameters) {
				if (parameters.At(0) is ObjectInstance handlers) {
					foreach (var kvp in handlers.GetOwnProperties()) {
						if (kvp.Key.IsString() && kvp.Value.Value is ScriptFunctionInstance) {
							var key = kvp.Key.AsString();
							AddHandler(key, (ScriptFunctionInstance)kvp.Value.Value);
						}
					}
				}
				_definitionBuilder.SetDefinesFold();
				RestrictProperties("when");
				return this;
			}

			private JsValue PartitionBy(JsValue thisValue, JsValue[] parameters) {
				if (parameters.At(0) is ScriptFunctionInstance partitionFunction) {
					_definitionBuilder.SetByCustomPartitions();


					_partitionFunction = partitionFunction;
					RestrictProperties("partitionBy");
					return this;
				}

				throw new ArgumentException("partitionBy");
			}

			private JsValue ForEachStream(JsValue thisValue, JsValue[] parameters) {
				_definitionBuilder.SetByStream();
				RestrictProperties("foreachStream");
				return this;
			}

			private JsValue OutputState(JsValue thisValue, JsValue[] parameters) {
				RestrictProperties("outputState");
				_definitionBuilder.SetOutputState();
				return this;
			}

			private JsValue OutputTo(JsValue thisValue, JsValue[] parameters) {
				if (parameters.Length != 1 && parameters.Length != 2)
					throw new ArgumentException("invalid number of parameters");
				if (!parameters.At(0).IsString())
#pragma warning disable CA2208 // ReSharper disable NotResolvedInText
					throw new ArgumentException("expected string value", "resultStream");
				if (parameters.Length == 2 && !parameters.At(1).IsString())
					throw new ArgumentException("expected string value", "partitionResultStreamPattern");
#pragma warning restore CA2208 // ReSharper restore NotResolvedInText
				_definitionBuilder.SetResultStreamNameOption(parameters.At(0).AsString());
				if (parameters.Length == 2)
					_definitionBuilder.SetPartitionResultStreamNamePatternOption(parameters.At(1).AsString());
				RestrictProperties("outputTo");
				return this;
			}

			private JsValue DefinesStateTransform(JsValue thisValue, JsValue[] parameters) {
				_definitionBuilder.SetDefinesStateTransform();
				return Undefined;
			}

			private JsValue FilterBy(JsValue thisValue, JsValue[] parameters) {
				if (parameters.At(0) is ScriptFunctionInstance fi) {
					_definitionBuilder.SetDefinesStateTransform();
					_transforms.Add((TransformType.Filter, fi));
					RestrictProperties("filterBy");
					return this;
				}

				throw new ArgumentException("expected function");
			}

			private JsValue TransformBy(JsValue thisValue, JsValue[] parameters) {
				if (parameters.At(0) is ScriptFunctionInstance fi) {
					_definitionBuilder.SetDefinesStateTransform();
					_transforms.Add((TransformType.Transform, fi));
					RestrictProperties("transformBy");
					return this;
				}

				throw new ArgumentException("expected function");
			}

			private JsValue OnEvent(JsValue thisValue, JsValue[] parameters) {
				if (parameters.Length != 2)
					throw new ArgumentException("invalid number of parameters");
				var eventName = parameters.At(0);
				var handler = parameters.At(1);
				if (!eventName.IsString())
					throw new ArgumentException("eventName");
				if (handler is not ScriptFunctionInstance fi)
					throw new ArgumentException("eventHandler");
				AddHandler(eventName.AsString(), fi);
				return Undefined;
			}

			private JsValue OnAny(JsValue thisValue, JsValue[] parameters) {
				if (parameters.Length != 1)
					throw new ArgumentException("invalid number of parameters");
				if (parameters.At(0) is not ScriptFunctionInstance fi)
					throw new ArgumentException("eventHandler");
				AddHandler("$any", fi);
				return Undefined;
			}
			
			private void AddHandler(string name, ScriptFunctionInstance handler) {
				switch (name) {
					case "$init":
						_init = handler;
						_definitionBuilder.SetDefinesStateTransform();
						break;
					case "$initShared":
						_definitionBuilder.SetIsBiState(true);
						_initShared = handler;
						break;
					case "$any":
						_any = handler;
						_definitionBuilder.AllEvents();
						break;
					case "$created":
						_createdHandlers.Add(handler);
						break;
					case "$deleted" when !_definitionBuilder.IsBiState:
						_definitionBuilder.SetHandlesStreamDeletedNotifications();
						_deleted = handler;
						break;
					case "$deleted" when _definitionBuilder.IsBiState:
						throw new Exception("Cannot handle deletes in bi-state projections");
					default:
						_definitionBuilder.NotAllEvents();
						_definitionBuilder.IncludeEvent(name);
						_handlers.Add(name, handler);
						break;
				}
			}

			private void RestrictProperties(string state) {
				var allowed = _availableProperties[state];
				var current = GetOwnPropertyKeys();
				foreach (var p in current) {
					if (!allowed.Contains(p.AsString())) {
						RemoveOwnProperty(p);
					}
				}

				foreach (var p in allowed) {
					if (!HasOwnProperty(p)) {
						_possibleProperties[p](this);
					}
				}
			}

			public JsValue InitializeState() {
				return _init == null ? new ObjectInstance(Engine) : _init.Invoke();
			}

			public JsValue InitializeSharedState() {
				return _initShared == null ? new ObjectInstance(Engine) : _initShared.Invoke();
			}

			public JsValue Handle(JsValue state, EventEnvelope eventEnvelope) {
				JsValue newState;
				if (_handlers.TryGetValue(eventEnvelope.EventType, out var handler)) {
					newState = handler.Invoke(state, FromObject(Engine, eventEnvelope));
				} else if (_any != null) {
					newState = _any.Invoke(state, FromObject(Engine, eventEnvelope));
				} else {
					newState = eventEnvelope.BodyRaw;
				}
				return newState == Undefined ? state : newState;
			}

			public JsValue TransformStateToResult(JsValue state) {
				foreach (var (type, transform) in _transforms) {
					switch (type)
					{
						case TransformType.Transform:
							state = transform.Invoke(state);
							break;
						case TransformType.Filter:
						{
							var result = transform.Invoke(state);
							if (!(result.IsBoolean() && result.AsBoolean()) || result == Null || result == Undefined) {
								return Null;
							}
							break;
						}
						case TransformType.None:
							throw new InvalidOperationException("Unknown transform type");
					}

					if (state == Null || state == Undefined)
						return Null;
				}

				return state;
			}

			JsValue FromAll(JsValue _, JsValue[] __) {
				_definitionBuilder.FromAll();
				RestrictProperties("fromAll");
				return this;
			}

			JsValue FromStreams(JsValue _, JsValue[] parameters) {
				IEnumerator<JsValue>? streams = null;
				try {
					streams = parameters.At(0).IsArray() ? parameters.At(0).AsArray().GetEnumerator() : parameters.AsEnumerable().GetEnumerator();
					while (streams.MoveNext()) {
						if (!streams.Current.IsString())
							throw new ArgumentException("streams");
						_definitionBuilder.FromStream(streams.Current.AsString());
					}
				} finally {
					streams?.Dispose();
				}

				RestrictProperties("fromStreams");
				return this;
			}


			JsValue SetOptions(JsValue thisValue, JsValue[] parameters) {
				var p0 = parameters.At(0);
				if (p0 is ObjectInstance opts) {
					foreach (var kvp in opts.GetOwnProperties()) {
						if (_setters.TryGetValue(kvp.Key.AsString(), out var setter)) {
							setter(_definitionBuilder, kvp.Value.Value);
						} else {
							throw new Exception($"Unrecognized option: {kvp.Key}");
						}
					}
				}

				return Undefined;
			}

			public JsValue GetPartition(EventEnvelope envelope) {
				if (_partitionFunction != null)
					return _partitionFunction.Invoke(envelope);
				return Null;
			}

			public void HandleCreated(JsValue state, EventEnvelope envelope) {
				for (int i = 0; i < _createdHandlers.Count; i++) {
					_createdHandlers[i].Call(Undefined, new[] { state, envelope });
				}
			}

			enum TransformType {
				None,
				Filter,
				Transform
			}

			public JsonInstance SwitchToExecutionMode() {
				RestrictProperties("execution");
				foreach (var globalProp in _definitionFunctions) {
					_engine.Global.RemoveOwnProperty(globalProp);
				}
				return _json;
			}

			public EventEnvelope CreateEnvelope(string partition, ResolvedEvent @event, string category) {
				_envelope.Partition = partition;
				_envelope.BodyRaw = @event.Data;
				_envelope.MetadataRaw = @event.Metadata;
				_envelope.StreamId = @event.EventStreamId;
				_envelope.EventId = @event.EventId.ToString("D");
				_envelope.EventType = @event.EventType;
				_envelope.LinkMetadataRaw = @event.PositionMetadata;
				_envelope.IsJson = @event.IsJson;
				_envelope.Category = category;
				_envelope.SequenceNumber = @event.EventSequenceNumber;
				return _envelope;
			}
			public sealed class EventEnvelope : ObjectInstance {
				private readonly JsonInstance _json;

				public string StreamId {
					set => FastSetProperty("streamId", new PropertyDescriptor(value, false, false, false));
				}
				public long SequenceNumber {
					set => FastSetProperty("sequenceNumber", new PropertyDescriptor(value, false, false, false));
				}

				public string EventType {
					get => Get("eventType").AsString();
					set => FastSetProperty("eventType", new PropertyDescriptor(value, false, false, false));
				}

				public JsValue Body {
					get {
						if (TryGetValue("body", out var value) && value is ObjectInstance oi)
							return oi;
						if (IsJson && TryGetValue("bodyRaw", out var raw)) {
							var args = new JsValue[1];
							args[0] = raw;
							var body = _json.Parse(JsValue.Undefined, args);
							FastSetProperty("body", PropertyDescriptor.ToPropertyDescriptor(Engine, body));
							return (ObjectInstance)body;
						}

						return Undefined;
					}
				}

				public bool IsJson {
					get => Get("isJson").AsBoolean();
					set => FastSetProperty("isJson", new PropertyDescriptor(value, false, false, false));
				}

				public string BodyRaw {
					get => Get("bodyRaw").AsString();
					set => FastSetProperty("bodyRaw", new PropertyDescriptor(value, false, false, false));
				}

				private JsValue Metadata {
					get {
						if (TryGetValue("metadata", out var value) && value is ObjectInstance oi)
							return oi;
						if (TryGetValue("metadataRaw", out var raw)) {
							var args = new JsValue[1];
							args[0] = raw;
							var metadata = _json.Parse(JsValue.Undefined, args);
							FastSetProperty("metadata", PropertyDescriptor.ToPropertyDescriptor(Engine, metadata));
							return (ObjectInstance)metadata;
						}

						return Undefined;
					}
				}

				public string MetadataRaw {
					set => FastSetProperty("metadataRaw", new PropertyDescriptor(value, false, false, false));
				}

				private JsValue LinkMetadata {
					get {
						if (TryGetValue("linkMetadata", out var value) && value is ObjectInstance oi)
							return oi;
						if (TryGetValue("linkMetadataRaw", out var raw)) {
							var args = new JsValue[1];
							args[0] = raw;
							var metadata = _json.Parse(JsValue.Undefined, args);
							FastSetProperty("linkMetadata", PropertyDescriptor.ToPropertyDescriptor(Engine, metadata));
							return (ObjectInstance)metadata;
						}

						return Undefined;
					}
				}

				public string LinkMetadataRaw {
					set => FastSetProperty("linkMetadataRaw", new PropertyDescriptor(value, false, false, false));
				}

				public string Partition {
					set => FastSetProperty("partition", new PropertyDescriptor(value, false, false, false));
				}

				public string Category {
					set => FastSetProperty("category", new PropertyDescriptor(value, false, false, false));
				}

				public string EventId {
					set => FastSetProperty("eventId", new PropertyDescriptor(value, false, false, false));
				}

				public EventEnvelope(Engine engine, JsonInstance json) : base(engine) {
					_json = json;
					PreventExtensions();
				}

				public override JsValue Get(JsValue property, JsValue receiver) {
					if (property == "body" || property == "data") {
						return Body;
					}

					if (property == "metadata") {
						return Metadata;
					}

					if (property == "linkMetadata") {
						return LinkMetadata;
					}
					return base.Get(property, receiver);
				}
			}

			public void HandleDeleted(string partition, bool isSoftDelete) {
				if (_deleted != null) {
					_deleted.Call(this, new JsValue[]{partition, isSoftDelete} );
				}
			}
		}


	}

}
