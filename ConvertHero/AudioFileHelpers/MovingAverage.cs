namespace ConvertHero.AudioFileHelpers
{
    /// <summary>
    /// This class allows the computation of a moving average across a signal.
    /// </summary>
    public class MovingAverage
    {
        /// <summary>
        /// The infinite impulse response filter used to compute the average.
        /// </summary>
        private IIR filter;

        /// <summary>
        /// The numerator coefficients.
        /// </summary>
        float[] b;

        /// <summary>
        /// The denominator coefficients.
        /// </summary>
        float[] a;

        /// <summary>
        /// Create a new instance of the MovingAverage class.
        /// </summary>
        /// <param name="size">
        /// The number of samples to average within the input signal.
        /// </param>
        public MovingAverage(int size)
        {
            this.b = new float[size];
            for(int i = 0; i < size; i++)
            {
                this.b[i] = 1.0f / size;
            }

            this.a = new float[] { 1.0f };

            this.filter = new IIR(this.b, this.a);
        }

        /// <summary>
        /// Compute the moving average across any sized signal.
        /// </summary>
        /// <param name="input">
        /// The input signal to smooth/average.
        /// </param>
        /// <returns>
        /// The smoothed signal.
        /// </returns>
        public float[] Compute(float[] input)
        {
            this.filter.Reset();
            return this.filter.Compute(input);
        }
    }
}
