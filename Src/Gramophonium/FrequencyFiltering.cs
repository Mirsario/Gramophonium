using System;
using System.Runtime.CompilerServices;

// Based on:
// https://www.codeproject.com/Tips/5070936/Lowpass-Highpass-and-Bandpass-Butterworth-Filters
// 🙃

namespace Gramophonium;

public struct HighPassFilterParameters
{
	public int NumSections;
	public float SampleRateRelativeCutoff;

	public HighPassFilterParameters(int numSections, float sampleRateRelativeCutoff)
	{
		NumSections = numSections;
		SampleRateRelativeCutoff = sampleRateRelativeCutoff;
	}
}

internal static class FrequencyFiltering
{
	private unsafe struct FirFilterData
	{
		public fixed float A[FirOrder];
		public fixed float Z[FirOrder];
	}

	private unsafe struct IirFilterData
	{
		public fixed float A[IirOrder];
		public fixed float Z[IirOrder];
	}

	private ref struct HighPassSection
	{
		public FirFilterData FirData;
		public IirFilterData IirData;
		public float Gain;
	}

	private const int FirOrder = 3;
	private const int IirOrder = 2;

	public static unsafe void HighPassFilter(ref Audio audio, in HighPassFilterParameters parameters)
	{
		// Initialize
		int numSections = parameters.NumSections;
		HighPassSection* sections = stackalloc HighPassSection[numSections];

		float cutoff = parameters.SampleRateRelativeCutoff * audio.SampleRate;

		for (int i = 0; i < numSections; i++) {
			PrepareHighPassSection(ref sections[i], cutoff, i + 1, numSections * 2, audio.SampleRate);
		}

		// Run
		var samples = audio.Samples;

		for (int i = 0; i < numSections; i++) {
			ref var section = ref sections[i];

			for (int j = 0; j < samples.Length; j++) {
				float sample = samples[j];

				sample *= section.Gain;
				sample = FirFilter(sample, ref section.FirData);
				sample = IirFilter(sample, ref section.IirData);

				audio.Samples[j] = sample;
			}
		}
	}

	private static unsafe void PrepareHighPassSection(ref HighPassSection data, float cutoffFrequencyHz, float k, float n, float fs)
	{
		// Pre-warp omegac and invert it
		float omegac = 1f / (2f * fs * MathF.Tan(MathF.PI * cutoffFrequencyHz / fs));

		// Compute zeta
		float zeta = -MathF.Cos(MathF.PI * (2f * k + n - 1f) / (2f * n));

		// FIR section
		data.FirData.A[0] = 4f * fs * fs;
		data.FirData.A[1] = -8f * fs * fs;
		data.FirData.A[2] = 4f * fs * fs;

		// IIR section
		// Normalize coefficients so that b0 = 1
		// and higher-order coefficients are scaled and negated
		float b0 = (4f * fs * fs) + (4f * fs * zeta / omegac) + (1f / (omegac * omegac));

		data.IirData.A[0] = ((2f / (omegac * omegac)) - (8f * fs * fs)) / (-b0);
		data.IirData.A[1] = ((4f * fs * fs) - (4f * fs * zeta / omegac) + (1f / (omegac * omegac))) / (-b0);

		// Etc.
		data.Gain = 1f / b0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe float FirFilter(float input, ref FirFilterData data)
	{
		float result = 0f;

		for (int t = FirOrder - 1; t >= 0; t--) {
			data.Z[t] = t != 0 ? data.Z[t - 1] : input;
			result += data.A[t] * data.Z[t];
		}

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe float IirFilter(float input, ref IirFilterData data)
	{
		float result = input;

		for (int t = 0; t < IirOrder; t++) {
			result += data.A[t] * data.Z[t];
		}

		for (int t = IirOrder - 1; t >= 0; t--) {
			data.Z[t] = t > 0 ? data.Z[t - 1] : result;
		}

		return result;
	}
}
