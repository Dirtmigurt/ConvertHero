namespace ConvertHero.AudioFileHelpers
{
    /// <summary>
    /// Class that can find the peaks of a spectrum
    /// </summary>
    public class SpectralPeaks
    {
        /// <summary>
        /// The peak detector that does all the work.
        /// </summary>
        private PeakDetection peakDetector;

        /// <summary>
        /// Initializes a new instance of the SpectralPeaks class.
        /// </summary>
        /// <param name="sampleRate">The sample rate of the audio file.</param>
        /// <param name="maxPeaks">The maximum number of peaks to find.</param>
        /// <param name="maxFrequency">The maximum frequency to look for peaks in</param>
        /// <param name="minFrequency">The minimum frequency to look for peaks in</param>
        /// <param name="magnitudeThreshold">The threhold that all peaks must meet.</param>
        /// <param name="type">How should the output be ordered</param>
        public SpectralPeaks(float sampleRate = 44100, int maxPeaks = 100, float maxFrequency = 5000, float minFrequency = 55, float magnitudeThreshold = 0, OrderByType type = OrderByType.Amplitude)
        {
            this.peakDetector = new PeakDetection(minFrequency, maxFrequency, magnitudeThreshold, maxPeaks, sampleRate / 2f, true, type);
        }

        /// <summary>
        /// Compute the spectral peaks.
        /// </summary>
        /// <param name="spectrum">The input spectrum.</param>
        /// <returns>The peak positions and amplitudes</returns>
        public (float[] positions, float[] amplitudes) Compute(float[] spectrum)
        {
            return this.peakDetector.Compute(spectrum);
        }
    }

    public enum OrderByType
    {
        Amplitude,
        Position
    }
}
