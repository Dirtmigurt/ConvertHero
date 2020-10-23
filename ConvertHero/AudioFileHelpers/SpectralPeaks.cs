using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class SpectralPeaks
    {
        private float sampleRate;
        private int maxPeaks;
        private float maxFrequency;
        private float minFrequency;
        private float magnitudeThreshold;
        private OrderByType orderByType;
        private PeakDetection peakDetector;
        public SpectralPeaks(float sampleRate = 44100, int maxPeaks = 100, float maxFrequency = 5000, float minFrequency = 55, float magnitudeThreshold = 0, OrderByType type = OrderByType.Amplitude)
        {
            this.sampleRate = sampleRate;
            this.maxPeaks = maxPeaks;
            this.maxFrequency = maxFrequency;
            this.minFrequency = minFrequency;
            this.magnitudeThreshold = magnitudeThreshold;
            this.orderByType = type;
            this.peakDetector = new PeakDetection(this.minFrequency, this.maxFrequency, this.magnitudeThreshold, this.maxPeaks, this.sampleRate / 2f, true, this.orderByType);
        }

        public (float[] frequencies, float[] magnitudes) Compute(float[] spectrum)
        {
            (float[] positions, float[] amplitudes) = this.peakDetector.Compute(spectrum);
            return (positions, amplitudes);
        }
    }

    public enum OrderByType
    {
        Amplitude,
        Position
    }
}
