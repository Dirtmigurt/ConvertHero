namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Linq;

    /// <summary>
    /// This algorithm implements a maximum filter for 1d signal using van Herk/Gil-Werman (HGW) algorithm.
    /// </summary>
    public class MaxFilter
    {
        /// <summary>
        /// The width of the filter
        /// </summary>
        private int width = 0;

        /// <summary>
        /// Half of the width of the filter.
        /// </summary>
        private int halfWidth = 0;

        /// <summary>
        /// Whether or not to use causal filter.
        /// </summary>
        private bool causal = false;

        /// <summary>
        /// What index the buffer fill starts at.
        /// </summary>
        private int bufferFillIndex = 0;

        /// <summary>
        /// Whether or not the buffer is filled.
        /// </summary>
        private bool filledBuffer = false;

        /// <summary>
        /// Holds the current max.
        /// </summary>
        private float currentMax = 0;

        /// <summary>
        /// The buffer array for the filter.
        /// </summary>
        private float[] buffer;

        /// <summary>
        /// Initialize a new instance of the MaxFilter class.
        /// </summary>
        /// <param name="width">
        /// the window size, has to be odd if the window is centered
        /// </param>
        /// <param name="causal">
        /// use casual filter (window is behind current element otherwise it is centered around)
        /// </param>
        public MaxFilter(int width, bool causal = false)
        {
            this.width = width;
            this.causal = causal;

            this.halfWidth = width;
            if (this.halfWidth % 2 == 0)
            {
                this.halfWidth++;
            }

            this.halfWidth = (this.halfWidth - 1) / 2;
            this.bufferFillIndex = causal ? 0 : this.halfWidth;
        }

        /// <summary>
        /// Compute the filter on the input.
        /// </summary>
        /// <param name="input">The input signal</param>
        /// <returns>The filtered output.</returns>
        public float[] Filter(float[] input)
        {
            int size = input.Length;
            float[] filtered = new float[size];

            int readIndex = 0;

            if(!this.filledBuffer)
            {
                if (this.bufferFillIndex == (this.causal ? 0 : this.halfWidth))
                {
                    this.currentMax = input[0];

                    // create buffer filled with this.currentMax
                    this.buffer = new float[this.width];
                    for(int i = 0; i < this.width; i++)
                    {
                        this.buffer[i] = this.currentMax;
                    }
                }

                int maxIndex = Math.Min(size, this.width - this.bufferFillIndex);
                for(int i = 0; i < maxIndex; i++)
                {
                    this.buffer[this.bufferFillIndex] = input[readIndex];
                    this.currentMax = Math.Max(input[readIndex], this.currentMax);
                    filtered[i] = this.currentMax;
                    readIndex++;
                    this.bufferFillIndex++;
                }

                this.filledBuffer = this.bufferFillIndex == this.width;
            }

            // Fill and compute max of the current circular buffer
            for(int j = readIndex; j < size; j++)
            {
                this.bufferFillIndex %= this.width;
                this.buffer[this.bufferFillIndex] = input[j];
                filtered[j] = this.buffer.Max();
                this.bufferFillIndex++;
            }

            return filtered;
        }

        /// <summary>
        /// Reset the filter.
        /// </summary>
        public void Reset()
        {
            this.buffer = null;
            this.filledBuffer = false;
            this.bufferFillIndex = 0;
        }
    }
}
