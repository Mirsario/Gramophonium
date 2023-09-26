using System;

namespace Gramophonium;

internal static class Logging
{
	[ThreadStatic]
	public static bool IsEnabledForThread;

	public static void Info(string message)
	{
		if (!IsEnabledForThread) {
			return;
		}

		Console.WriteLine(message);
	}
}
