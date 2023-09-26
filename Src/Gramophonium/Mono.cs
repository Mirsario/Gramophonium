using System;

#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope

namespace Gramophonium;

internal static class Mono
{
	public static unsafe void ConvertToMono(ref Audio audio)
	{
		ConvertToMono(in audio, out var output);

		audio = output;
	}

	public static unsafe void ConvertToMono(in Audio input, out Audio output)
	{
		if (input.Channels <= 1) {
			output = input.Copy();
			return;
		}

		output = input with {
			Channels = 1,
			Samples = new float[input.SamplesPerChannel],
		};

		delegate*<in Audio, in Audio, void> function = input.Channels switch {
			2 when input.Layout == AudioLayout.Interleaved => &ConvertInterleavedStereoToMono,
			2 when input.Layout == AudioLayout.NonInterleaved => &ConvertNonInterleavedStereoToMono,
			> 2 when input.Layout == AudioLayout.Interleaved => &ConvertInterleavedArbitraryToMono,
			> 2 when input.Layout == AudioLayout.NonInterleaved => &ConvertNonInterleavedArbitraryToMono,
			_ => throw new InvalidOperationException(),
		};

		function(in input, in output);
	}

	private static void ConvertInterleavedStereoToMono(in Audio input, in Audio output)
	{
		var inputSamples = input.Samples;
		var outputSamples = output.Samples;

		for (int i = 0, j = 0; i < outputSamples.Length; i++) {
			outputSamples[i] = (inputSamples[j++] + inputSamples[j++]) * 0.5f;
		}
	}

	private static void ConvertNonInterleavedStereoToMono(in Audio input, in Audio output)
	{
		var inputSamples = input.Samples;
		var outputSamples = output.Samples;

		int o = 0;
		int i1 = 0;
		int i2 = inputSamples.Length / 2;

		while (o < outputSamples.Length) {
			outputSamples[o++] = (inputSamples[i1++] + inputSamples[i2++]) * 0.5f;
		}
	}

	private static void ConvertInterleavedArbitraryToMono(in Audio input, in Audio output)
	{
		var inputSamples = input.Samples;
		var outputSamples = output.Samples;
		int channelCount = input.Channels;

		for (int channel = 0; channel < channelCount; channel++) {
			for (int i = 0, j = channel; i < outputSamples.Length; i++, j += channelCount) {
				outputSamples[i] += inputSamples[j];
			}
		}

		float volumeFactor = 1f / channelCount;

		for (int i = 0; i < outputSamples.Length; i++) {
			outputSamples[i] *= volumeFactor;
		}
	}

	private static void ConvertNonInterleavedArbitraryToMono(in Audio input, in Audio output)
	{
		var inputSamples = input.Samples;
		var outputSamples = output.Samples;
		int channelCount = input.Channels;
		int samplesPerChannel = input.SamplesPerChannel;

		for (int channel = 0; channel < channelCount; channel++) {
			for (int i = 0, j = channel * samplesPerChannel; i < outputSamples.Length; i++, j++) {
				outputSamples[i] += inputSamples[j];
			}
		}

		float volumeFactor = 1f / channelCount;

		for (int i = 0; i < outputSamples.Length; i++) {
			outputSamples[i] *= volumeFactor;
		}
	}
}
