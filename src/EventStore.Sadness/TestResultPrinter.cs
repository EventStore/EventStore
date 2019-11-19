using System;

namespace EventStore.Sadness {
	internal static class TestResultPrinter {
		public static void Pass(string name) => Console.WriteLine($"{Palette.Green} ✔ {Palette.Reset} {name}");

		public static void Fail(string name, Exception ex) =>
			Console.WriteLine($"{Palette.Red} 🖕 {name}{Environment.NewLine}{ex}{Palette.Reset}");

		public static void FailInitialize(string name, Exception ex) =>
			Console.WriteLine(
				$"{Palette.Red} 🖕 {name} initialization failed.{Environment.NewLine}{ex}{Palette.Reset}");

		public static void FailCleanup(string name, Exception ex) =>
			Console.WriteLine($"{Palette.Red} 🖕 {name} cleanup failed.{Environment.NewLine}{ex}{Palette.Reset}");
	}
}
