namespace ConvertHero.AudioFileHelpers
{
    using System;

    /// <summary>
    /// Compute the energy in rectangular frequency bands of a spectrum.
    /// </summary>
    public class FrequencyBands
    {
        /// <summary>
        /// The default frequency bands.
        /// </summary>
        private float[] freqBands = new float[] {   0.0f, 50.0f, 100.0f, 150.0f, 200.0f, 300.0f, 400.0f, 510.0f, 630.0f, 770.0f,
                                                    920.0f, 1080.0f, 1270.0f, 1480.0f, 1720.0f, 2000.0f, 2320.0f, 2700.0f,
                                                    3150.0f, 3700.0f, 4400.0f, 5300.0f, 6400.0f, 7700.0f, 9500.0f, 12000.0f,
                                                    15500.0f, 20500.0f, 27000.0f };

        /// <summary>
        /// The sample rate of the signal.
        /// </summary>
        private int sampleRate;

        /// <summary>
        /// Initializes a new instance of the FrequencyBands class.
        /// </summary>
        /// <param name="bands">
        /// The bands
        /// </param>
        /// <param name="sampleRate"></param>
        public FrequencyBands(int sampleRate = 44100, float[] bands = null)
        {
            if (bands != null)
            {
                this.freqBands = bands;
            }

            this.sampleRate = sampleRate;
        }

        /// <summary>
        /// Compute the energy in rectangular frequency bands of the spectrum.
        /// </summary>
        /// <param name="spectrum">
        /// The spectrum of a signal. (Magnitude spectrum).
        /// </param>
        /// <returns>
        /// The energy in each frequency band.
        /// </returns>
        public float[] Compute(float[] spectrum)
        {
            float frequencyScale = (this.sampleRate / 2f) / (spectrum.Length - 1);
            int nBands = this.freqBands.Length - 1;
            float[] bands = new float[nBands];
            for(int i = 0; i < nBands; i++)
            {
                int startBin = (int)(this.freqBands[i] / frequencyScale + 0.5);
                int endBin = (int)(this.freqBands[i + 1] / frequencyScale + 0.5);
                if (startBin >= spectrum.Length)
                {
                    break;
                }

                endBin = Math.Min(endBin, spectrum.Length);
                for(int j = startBin; j < endBin; j++)
                {
                    bands[i] += spectrum[j] * spectrum[j];
                }
            }

            return bands;
        }
    }
}
