namespace Gramophonium;

internal static class Volume
{
	public static unsafe void NormalizeVolume(ref Audio audio, float targetVolume)
	{
		// Prepare
		float maxVolume = float.MinValue;

		for (int i = 0; i < audio.Samples.Length; i++) {
			float sample = audio.Samples[i];

			if (sample > maxVolume) {
				maxVolume = sample;
			}
		}
		
		// Apply
		float factor = targetVolume / maxVolume;

		for (int i = 0; i < audio.Samples.Length; i++) {
			audio.Samples[i] *= factor;
		}
	}
}
