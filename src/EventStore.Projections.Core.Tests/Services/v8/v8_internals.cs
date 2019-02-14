using System;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Projections.Core.v8;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class v8_internals {
		private static readonly string _jsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prelude");
		private Action<int, Action> _cancelCallbackFactory;
		private Js1.CommandHandlerRegisteredDelegate _commandHandlerRegisteredCallback;
		private Js1.ReverseCommandHandlerDelegate _reverseCommandHandlerDelegate;

		[Test, Explicit, Category("v8"), Category("Manual")]
		public void long_execution_of_non_v8_code_does_not_crash() {
			Assert.Throws<Js1Exception>(() => {
				_cancelCallbackFactory = (timeout, action) => ThreadPool.QueueUserWorkItem(state => {
					Console.WriteLine("Calling a callback in " + timeout + "ms");
					Thread.Sleep(timeout);
					action();
				});
				Action<string, object[]> logger = (m, _) => Console.WriteLine(m);


				Func<string, Tuple<string, string>> getModuleSource = name => {
					var fullScriptFileName = Path.GetFullPath(Path.Combine(_jsPath, name + ".js"));
					var scriptSource = File.ReadAllText(fullScriptFileName, Helper.UTF8NoBom);
					return Tuple.Create(scriptSource, fullScriptFileName);
				};


				var preludeSource = getModuleSource("1Prelude");
				var prelude = new PreludeScript(preludeSource.Item1, preludeSource.Item2, getModuleSource,
					_cancelCallbackFactory, logger);
				try {
					//var cancelToken = 123;
					prelude.ScheduleTerminateExecution();
					Thread.Sleep(500);
					_commandHandlerRegisteredCallback = (name, handle) => { };
					_reverseCommandHandlerDelegate = (name, body) => { };
					Js1.CompileQuery(
						prelude.GetHandle(), "log(1);", "fn", _commandHandlerRegisteredCallback,
						_reverseCommandHandlerDelegate);

					prelude.CancelTerminateExecution();
				} catch {
					prelude.Dispose(); // clean up unmanaged resources if failed to create
					throw;
				}
			});
		}
	}
}
