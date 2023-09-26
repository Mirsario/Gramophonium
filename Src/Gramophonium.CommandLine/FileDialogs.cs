using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NativeFileDialogSharp;

namespace GramophoniumCLI;

internal static class FileDialogs
{
	public static bool TryOpenFile(out string path, string? filter = null, string? defaultPath = null)
	{
		var result = Dialog.FileOpen(filter, defaultPath);

		path = result.Path;

		return result.IsOk;
	}

	public static bool TryOpenFiles(out ReadOnlySpan<string> paths, string? filter = null, string? defaultPath = null)
	{
		var result = Dialog.FileOpenMultiple(filter, defaultPath);

		paths = CollectionsMarshal.AsSpan((List<string>)result.Paths);

		return result.IsOk && paths.Length != 0;
	}

	public static bool TrySaveFile(out string path, string? filter = null, string? defaultPath = null)
	{
		var result = Dialog.FileSave(filter, defaultPath);

		path = result.Path;

		return result.IsOk;
	}

	public static bool TryPickDirectory(out string path, string? defaultPath = null)
	{
		var result = Dialog.FolderPicker(defaultPath);

		path = result.Path;

		return result.IsOk;
	}
}
