using System;

#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope

namespace Gramophonium;

internal static class Sampling
{
	public static unsafe void ResampleNonInterleaved(ref Audio audio, int targetSampleRate)
	{
		ResampleNonInterleaved(in audio, out var output, targetSampleRate);

		audio = output;
	}

	public static void ResampleNonInterleaved(in Audio input, out Audio output, int targetSampleRate)
	{
		if (input.Layout != AudioLayout.NonInterleaved) {
			throw new InvalidOperationException("Expected a non-interleaved audio track.");
		}

		output = input with {
			Samples = new float[(int)MathF.Floor(input.LengthInSeconds * targetSampleRate) * input.Channels],
			SampleRate = targetSampleRate
		};

		for (int i = 0; i < input.Channels; i++) {
			LowerSamplingRate(input[i], output[i]);
		}
	}

	private static void LowerSamplingRate(ReadOnlySpan<float> input, Span<float> output)
	{
		double indexConversion = (output.Length - 1) / (float)(input.Length - 1);

		for (int i = 0; i < input.Length; i++) {
			int index = (int)Math.Floor(i * indexConversion);

			output[index] += input[i];
		}

		float volumeScale = output.Length / (float)input.Length;

		for (int i = 0; i < output.Length; i++) {
			output[i] *= volumeScale;
		}
	}
}
