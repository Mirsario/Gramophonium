using System;

namespace Gramophonium;

public struct GramophoneSettings
{
	public static readonly GramophoneSettings Default = new();

	/// <summary>
	/// Whether the output audio track should be converted to mono.
	/// <para/> Default: <c>true</c>
	/// </summary>
	public bool ConvertToMono = true;

	/// <summary>
	/// Whether to log information about ongoing actions to the console.
	/// <para/> Default: <c>false</c>
	/// </summary>
	public bool LogToConsole = false;

	/// <summary>
	/// The level to bring peak audio level to, or null to skip this step.
	/// <para/> Default: <c>0.95</c>
	/// </summary>
	public float? VolumeNormalization = 0.95f;

	/// <summary>
	/// The sampling rate to convert the output track to, or null to leave it as input's.
	/// <para/> Default: <c>11025</c>
	/// </summary>
	public int? TargetSamplingRate = 11025;

	//TODO: Implement this.
	/*
	/// <summary>
	/// The amount of seconds of silence to ensure at the start and the end of the output track, before any noise is added.
	/// <br/> Set both values to zero to just trim input tracks.
	/// <br/> Set to null to skip this step.
	/// <para/> Default: <c>(1.0f, 1.0f)</c>
	/// </summary>
	public (float Start, float End)? SilenceLength = (1.0f, 1.0f);
	*/

	/// <summary>
	/// The layout to use for the output track, or null to use the input layout.
	/// <para/> Default: <c>null</c>
	/// </summary>
	public AudioLayout? TargetLayout;

	/// <summary>
	/// <br/> Parameters for high-pass filtering, or null to skip it.
	/// <para/> Default: <c>new(numSections: 32, sampleRateRelativeCutoff: 425f / 48000f)</c>
	/// </summary>
	public HighPassFilterParameters? HighPassFiltering = new(numSections: 32, sampleRateRelativeCutoff: 425f / 48000f);

	public GramophoneSettings() { }
}

public static class Gramophone
{
	public static unsafe void Process(in Audio input, out Audio output, GramophoneSettings settings)
	{
		if (!input.IsValid) {
			throw new ArgumentException($"Invalid {nameof(Audio)} structure passed as input.");
		}

		Logging.IsEnabledForThread = settings.LogToConsole;

		Logging.Info($"\tCopying input...");

		output = input.Copy();

		if (output.Layout == AudioLayout.Interleaved) {
			Logging.Info($"\tDeinterleaving input...");
			output.Deinterleave();
		}

		if (settings.ConvertToMono) {
			Logging.Info("\tConverting audio track to mono...");
			Mono.ConvertToMono(ref output);
		}

		if (settings.HighPassFiltering is HighPassFilterParameters highPassFilterParameters) {
			Logging.Info("\tApplying high-pass filtering...");
			FrequencyFiltering.HighPassFilter(ref output, highPassFilterParameters);
		}

		if (settings.TargetSamplingRate is int targetSamplingRate) {
			Logging.Info($"\tResampling audio track from {input.SampleRate} to {targetSamplingRate}hz...");
			Sampling.ResampleNonInterleaved(ref output, targetSamplingRate);
		}

		if (settings.VolumeNormalization is float volumeNormalization) {
			Logging.Info($"\tNormalizing volume...");
			Volume.NormalizeVolume(ref output, volumeNormalization);
		}

		if ((settings.TargetLayout ?? input.Layout) == AudioLayout.Interleaved && output.Layout != AudioLayout.Interleaved) {
			Logging.Info($"\tInterleaving output...");
			output.Interleave();
		}

		Logging.Info($"Processing completed.");
	}
}
