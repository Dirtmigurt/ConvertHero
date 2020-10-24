namespace ConvertHero.AudioFileHelpers
{
    using System.Collections.Generic;

    /// <summary>
    /// A fancier version of the Flux spectral onset detector.
    /// </summary>
    public class SuperFlux
    {
        /// <summary>
        /// The bin width of the max filter.
        /// </summary>
        int binWidth = 0;

        /// <summary>
        /// Whether or not the max filter is causal.
        /// </summary>
        bool causal = false;

        /// <summary>
        /// The width of a frame.
        /// </summary>
        int frameWidth = 0;

        /// <summary>
        /// The previous frames passed into compute.
        /// </summary>
        List<float[]> bands = new List<float[]>();

        /// <summary>
        /// The Max Filter.
        /// </summary>
        MaxFilter maxFilter;

        /// <summary>
        /// Initializes a new instance of the SuperFlux class.
        /// </summary>
        /// <param name="binWidth">The bin width of the max filter.</param>
        /// <param name="frameWidth">The width of a frame</param>
        /// <param name="causal">Whether or not the max filter is causal</param>
        public SuperFlux(int binWidth, int frameWidth, bool causal = false)
        {
            this.binWidth = binWidth;
            this.frameWidth = frameWidth;
            this.causal = causal;
            this.maxFilter = new MaxFilter(this.binWidth, this.causal);
        }
        
        /// <summary>
        /// Compute the difference between this band the the previous bands in the frame.
        /// </summary>
        /// <param name="newBand">
        /// The new band (also called frame/window)
        /// </param>
        /// <returns>
        /// An indication of how different this band is that the previous ones.
        /// </returns>
        public float Compute(float[] newBand)
        {
            this.bands.Add(newBand);
            while(this.bands.Count > this.frameWidth + 1 && this.bands.Count > 0)
            {
                this.bands.RemoveAt(0);
            }

            if (this.bands.Count < this.frameWidth + 1)
            {
                return 0;
            }

            int nFrames = this.bands.Count;
            int nBands = this.bands[0].Length;
            
            float[] maxsBuffer = new float[nBands];

            // buffer for differences
            float diffs = 0;
            maxsBuffer = this.maxFilter.Filter(this.bands[0]);

            for(int j = 0; j < nBands; j++)
            {
                float cur_diff = this.bands[this.frameWidth][j] - maxsBuffer[j];
                if(cur_diff > 0.0f)
                {
                    diffs += cur_diff;
                }
            }

            return diffs;
        }
    }
}
