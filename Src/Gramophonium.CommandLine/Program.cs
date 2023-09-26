using System;
using System.IO;
using System.Threading;
using Gramophonium;
using NVorbis;
using OggVorbisEncoder;

namespace GramophoniumCLI;

internal unsafe static class Program
{
	private const string FileFilter = @"ogg";
	private const string OggExtension = ".ogg";
	private const string DefaultFileSuffix = "_Disc";
	private const string DefaultFileSuffixWithExtension = DefaultFileSuffix + OggExtension;

	static void Main()
	{
		try {
			Run();
		}
		catch (Exception e) {
			Console.WriteLine($"{e.GetType().Name}: {e.Message}\r\n");

			Environment.ExitCode = e.HResult;

			Thread.Sleep(1000);
			return;
		}
	}

	private static void Run()
	{
		if (!TryGetInputOutputPaths(out var filePathPairs)) {
			return;
		}

		foreach (var pair in filePathPairs) {
			HandleFile(pair.InputPath, pair.OutputPath);
		}
	}

	private static bool TryGetInputOutputPaths(out ReadOnlySpan<(string InputPath, string OutputPath)> filePairs)
	{
		filePairs = default;

		// Get input paths
		Console.WriteLine("Please choose the input file(s).");

		if (!FileDialogs.TryOpenFiles(out var inputPaths, FileFilter)) {
			return false;
		}

		var mutableFilePairs = new (string InputPath, string OutputPath)[inputPaths.Length];

		for (int i = 0; i < mutableFilePairs.Length; i++) {
			mutableFilePairs[i] = (inputPaths[i], null!);
		}

		// Get output paths
		if (mutableFilePairs.Length == 1) {
			// Single file
			Console.WriteLine("Please choose the output file.");

			string fileInputPath = mutableFilePairs[0].InputPath;
			string? firstFileExtension = Path.GetExtension(fileInputPath);
			string? defaultSavePath = string.Concat(fileInputPath.AsSpan(0, fileInputPath.Length - firstFileExtension.Length), DefaultFileSuffixWithExtension);

			if (!FileDialogs.TrySaveFile(out string fileOutputPath, FileFilter, defaultSavePath)) {
				return false;
			}

			if (string.IsNullOrEmpty(Path.GetExtension(fileOutputPath))) {
				fileOutputPath = Path.ChangeExtension(fileOutputPath, OggExtension);
			}

			mutableFilePairs[0] = (fileInputPath, fileOutputPath);
		} else {
			// Output directory
			Console.WriteLine("Please choose the output directory.");

			if (!FileDialogs.TryPickDirectory(out string outputPath)) {
				return false;
			}

			Directory.CreateDirectory(outputPath);

			for (int i = 0; i < mutableFilePairs.Length; i++) {
				string fileInputPath = mutableFilePairs[i].InputPath;
				ReadOnlySpan<char> inputFileName = Path.GetFileNameWithoutExtension(fileInputPath.AsSpan());
				string fileOutputPath = string.Concat(outputPath, "/", inputFileName, DefaultFileSuffixWithExtension);

				mutableFilePairs[i] = (fileInputPath, fileOutputPath);
			}
		}

		filePairs = mutableFilePairs;

		return true;
	}

	private static void HandleFile(string inputPath, string outputPath)
	{
		// Read
		Console.WriteLine($"Reading input OGG - '{inputPath}'...");

		Audio inputAudio;

		using (var inputStream = File.OpenRead(inputPath)) {
			ReadOgg(inputStream, out inputAudio);
		}

		// Process
		var settings = new GramophoneSettings {
			ConvertToMono = true,
			LogToConsole = true,
			TargetLayout = AudioLayout.NonInterleaved,
			TargetSamplingRate = 11025,
		};

		//settings.HighPassFiltering = settings.HighPassFiltering!.Value with { NumSections = 16 };

		Gramophone.Process(inputAudio, out var outputAudio, settings);

		// Write
		Console.WriteLine($"Writing output OGG - '{outputPath}'...");

		string tempOutputPath = $"{outputPath}.tmp";
		bool useTempFile = File.Exists(outputPath);
		string usedOutputPath = useTempFile ? tempOutputPath : outputPath;

		if (useTempFile) {
			RunFileOperation(() => File.Delete(tempOutputPath));
		}

		FileStream outputStream = null!;

		RunFileOperation(() => outputStream = File.OpenWrite(usedOutputPath));

		WriteOgg(outputStream, in outputAudio, qualityFactor: 0.1f);
		outputStream.Dispose();

		if (useTempFile) {
			RunFileOperation(() => File.Delete(outputPath));
			RunFileOperation(() => File.Move(tempOutputPath, outputPath));
		}
	}

	private static void RunFileOperation(Action operation)
	{
		while (true) {
			try {
				operation();
				break;
			}
			catch (Exception e) {
				Console.WriteLine($"File system error: {e.Message}");
				Console.WriteLine("Retrying in 5 seconds...");
				Thread.Sleep(5000);
			}
		}
	}

	private static void ReadOgg(Stream inputStream, out Audio audio)
	{
		using var oggReader = new VorbisReader(inputStream, closeOnDispose: false);

		float[] samples = new float[oggReader.TotalSamples * oggReader.Channels];

		oggReader.ReadSamples(samples, 0, samples.Length);

		audio = new Audio(samples, oggReader.Channels, oggReader.SampleRate, AudioLayout.Interleaved);
	}

	private static void WriteOgg(Stream outputStream, in Audio audio, float qualityFactor)
	{
		if (audio.Layout != AudioLayout.NonInterleaved) {
			throw new InvalidOperationException("Expected non-interleaved audio!");
		}

		const int WriteBufferSize = 512;

		var oggStream = new OggStream((int)DateTime.Now.Ticks);

		void FlushPages(bool force)
		{
			while (oggStream.PageOut(out var page, force)) {
				outputStream.Write(page.Header);
				outputStream.Write(page.Body);
			}
		}

		// Write headers
		var info = VorbisInfo.InitVariableBitRate(audio.Channels, audio.SampleRate, qualityFactor);
		var comments = new Comments();

		oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
		oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
		oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));

		FlushPages(force: true);

		var processingState = ProcessingState.Create(info);

		// Samples with length aligned to write buffer size.
		int baseSamplesPerChannel = audio.Samples.Length / audio.Channels;
		int samplesPerChannel = (int)MathF.Ceiling(baseSamplesPerChannel / (float)WriteBufferSize) * WriteBufferSize;
		float[][] outputSamples = new float[audio.Channels][];

		for (int i = 0; i < audio.Channels; i++) {
			float[] channelSamples = outputSamples[i] = new float[samplesPerChannel];

			audio[i].CopyTo(channelSamples);
		}

		// Write body
		for (int readIndex = 0; readIndex <= samplesPerChannel; readIndex += WriteBufferSize) {
			if (readIndex == samplesPerChannel) {
				processingState.WriteEndOfStream();
			} else {
				processingState.WriteData(outputSamples, WriteBufferSize, readIndex);
			}

			while (!oggStream.Finished && processingState.PacketOut(out var packet)) {
				oggStream.PacketIn(packet);

				FlushPages(force: false);
			}
		}

		// Flush for the last time
		FlushPages(force: true);
	}
}
