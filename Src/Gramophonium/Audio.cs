using System;
using System.Buffers;

namespace Gramophonium;

public enum AudioLayout : byte
{
	NonInterleaved,
	Interleaved,
}

/// <summary>
/// A simple stack-allocated container for audio.
/// </summary>
public ref struct Audio
{
	/// <summary> The amount of channels of this audio track. </summary>
	public int Channels;
	/// <summary> The sample rate of this audio track. </summary>
	public int SampleRate;
	/// <summary> Whether values values of the <see cref="Samples"/> span are interleaved or not. </summary>
	public AudioLayout Layout;
	/// <summary> All samples of all of this track's channels. Set <see cref="Layout"/> according to this data. </summary>
	public Span<float> Samples;

	/// <summary> <code>Samples.Length / Channels</code> </summary>
	public int SamplesPerChannel => Samples.Length / Channels;
	/// <summary> <code>SamplesPerChannel / (float)SampleRate</code> </summary>
	public float LengthInSeconds => SamplesPerChannel / (float)SampleRate;
	/// <summary> Checks if this structure has been initialized correctly. </summary>
	public bool IsValid => Channels > 0 && SampleRate > 0 && Samples != default && Layout is AudioLayout.Interleaved or AudioLayout.NonInterleaved;

	public Span<float> this[int channel] {
		get {
			if (Layout != AudioLayout.NonInterleaved) {
				throw new InvalidOperationException("The Audio 'channel' indexer can only be used with non-interleaved tracks.");
			}

			if (channel < 0 || channel >= Channels) {
				throw new IndexOutOfRangeException();
			}

			return Samples.Slice(channel * SamplesPerChannel, SamplesPerChannel);
		}
	}

	public Audio() { }

	public Audio(Span<float> samples, int channels, int sampleRate, AudioLayout layout)
	{
		Samples = samples;
		Channels = channels;
		SampleRate = sampleRate;
		Layout = layout;
	}

	/// <summary>
	/// Converts samples to interleaved storage if <see cref="IsInterleaved"/> is false.
	/// </summary>
	public void Interleave()
	{
		if (Layout == AudioLayout.Interleaved) {
			return;
		}

		if (Channels > 1) {
			float[] copy = ArrayPool<float>.Shared.Rent(Samples.Length);
			int numSamples = Samples.Length;
			int numChannels = Channels;
			int numSamplesPerChannel = SamplesPerChannel;

			Samples.CopyTo(copy);

			for (int i = 0; i < numSamples; i++) {
				int div = Math.DivRem(i, numChannels, out int rem);

				Samples[i] = copy[div + rem * numSamplesPerChannel];
			}

			ArrayPool<float>.Shared.Return(copy);
		}

		Layout = AudioLayout.Interleaved;
	}

	/// <summary>
	/// Converts samples to interleaved storage if <see cref="IsInterleaved"/> is false.
	/// </summary>
	public void Deinterleave()
	{
		if (Layout == AudioLayout.NonInterleaved) {
			return;
		}

		if (Channels > 1) {
			float[] copy = ArrayPool<float>.Shared.Rent(Samples.Length);
			int numSamples = Samples.Length;
			int numChannels = Channels;
			int numSamplesPerChannel = SamplesPerChannel;

			Samples.CopyTo(copy);

			for (int i = 0; i < numSamples; i++) {
				int div = Math.DivRem(i, numSamplesPerChannel, out int rem);

				Samples[i] = copy[div + rem * numChannels];
			}

			ArrayPool<float>.Shared.Return(copy);
		}

		Layout = AudioLayout.NonInterleaved;
	}

	/// <summary>
	/// Creates a copy of this audio track, with a new heap-allocation for the samples array.
	/// </summary>
	public Audio Copy()
	{
		return this with {
			Samples = Samples.ToArray(),
		};
	}
}
