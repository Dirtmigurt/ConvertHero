namespace ConvertHero.AudioFileHelpers
{
    /// <summary>
    /// The supported HFC Techniques.
    /// </summary>
    public enum HFCTechnique
    {
        Masri,
        Jensen,
        Brossier
    }

    /// <summary>
    /// Class for computing the high frequency content of a spectrum
    /// </summary>
    public class HighFrequencyContent
    {
        /// <summary>
        /// What technique should be used to compute the HFC?
        /// </summary>
        private HFCTechnique type;

        /// <summary>
        /// The sample rate of the signal.
        /// </summary>
        private int sampleRate;

        /// <summary>
        /// The highest frequency to include in the computation.
        /// </summary>
        private double cutoffFrequency;

        /// <summary>
        /// Initializes a new instance of the HighFrequencyContent class.
        /// </summary>
        /// <param name="type">
        /// What HFC Technique should be used.
        /// </param>
        /// <param name="sampleRate">
        /// The sample rate of the signal.
        /// </param>
        /// <param name="cutoffFrequency">
        /// The highest frequency to include in the computation
        /// 7040 == A8 which is on the upper bound of useful musical notes.
        /// </param>

        public HighFrequencyContent(HFCTechnique type, int sampleRate, double cutoffFrequency = 7040)
        {
            this.type = type;
            this.sampleRate = sampleRate;
            this.cutoffFrequency = cutoffFrequency;
        }

        /// <summary>
        /// Compute how much high frequency content is present in the input spectrum.
        /// </summary>
        /// <param name="spectrum">
        /// The input spectrum
        /// </param>
        /// <returns>
        /// The amount of high frequency content in the spectrum.
        /// </returns>
        public float Compute(float[] spectrum)
        {
            float hertzPerBin = 0;
            if(spectrum.Length > 1)
            {
                hertzPerBin = (this.sampleRate / 2.0f) / (spectrum.Length - 1f);
            }

            int maxBin = (int)(this.cutoffFrequency / hertzPerBin);
            float hfc = 0f;
            switch(this.type)
            {
                case HFCTechnique.Masri:
                    for(int i = 0; i < spectrum.Length && i <= maxBin; i++)
                    {
                        hfc += i * hertzPerBin * spectrum[i] * spectrum[i];
                    }
                    break;
                case HFCTechnique.Jensen:
                    for (int i = 0; i < spectrum.Length && i <= maxBin; i++)
                    {
                        hfc += i * hertzPerBin * i * hertzPerBin * spectrum[i];
                    }
                    break;
                case HFCTechnique.Brossier:
                default:
                    for (int i = 0; i < spectrum.Length && i <= maxBin; i++)
                    {
                        hfc += i * hertzPerBin * spectrum[i];
                    }
                    break;
            }

            return hfc;
        }
    }
}
