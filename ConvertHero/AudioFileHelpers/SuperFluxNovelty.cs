namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// This implementation is based on the available reference implementation in python [1]. 
    /// The algorithm computes spectrum of the input signal, summarizes it into triangular band energies, and computes a onset detection function based on spectral flux tracking spectral trajectories with a maximum filter (SuperFluxNovelty).
    /// References: 
    ///     [1] https://github.com/CPJKU/SuperFlux
    /// </summary>
    public class SuperFluxNovelty
    {
        private int binWidth;
        private int frameWidth;
        MaxFilter maxFilter;

        /// <summary>
        /// Initializes a new instance of the SuperFluxNovelty class for computing the novelty curve of an input signal using the super flux algorithm.
        /// </summary>
        /// <param name="binWidth">filter width (number of frequency bins)</param>
        /// <param name="frameWidth">differentiation offset (compute the difference with the N-th previous frame)</param>
        public SuperFluxNovelty(int binWidth = 3, int frameWidth = 2)
        {
            this.binWidth = binWidth;
            this.frameWidth = frameWidth;
            this.maxFilter = new MaxFilter(this.binWidth);
        }

        /// <summary>
        /// Compute the novelty curve for the input spectrum bands.
        /// </summary>
        /// <param name="bands">
        /// The input spectral bands.
        /// </param>
        /// <returns>
        /// The super flux novelty curve.
        /// </returns>
        public float Compute(List<float[]> bands)
        {
            if (bands == null || bands.Count <= 0)
            {
                throw new Exception($"SuperFluxNovelty: Input bands cannot be null or empty.");
            }
            int nFrames = bands.Count();
            int nBands = bands[0].Length;

            if (this.frameWidth >= nFrames)
            {
                throw new Exception($"SuperFluxNovelty: Not enough frames ({nFrames}) for the specified frameWidth ({this.frameWidth})");
            }

            float cur_diff = 0;
            float diffs = 0;
            for(int i = this.frameWidth; i < nFrames; i++)
            {
                float[] maxSBuffer = this.maxFilter.Filter(bands[i - frameWidth]);
                cur_diff = 0;

                for(int j = 0; j < nBands; j++)
                {
                    cur_diff = bands[i][j] - maxSBuffer[j];
                    if (cur_diff > 0)
                    {
                        diffs += cur_diff;
                    }
                }
            }

            return diffs;
        }
    }
}
