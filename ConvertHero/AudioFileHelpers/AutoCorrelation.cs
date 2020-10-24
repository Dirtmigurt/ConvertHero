namespace ConvertHero.AudioFileHelpers
{
    using MathNet.Numerics;
    using System;

    /// <summary>
    /// "This algorithm computes the autocorrelation vector of a signal.
    /// It uses the version most commonly used in signal processing, which doesn't remove "
    /// the mean from the observations.\n"
    /// Using the 'generalized' option this algorithm computes autocorrelation as described in [3].
    ///
    /// "References:"
    ///  [1] Autocorrelation -- from Wolfram MathWorld,
    ///  http://mathworld.wolfram.com/Autocorrelation.html
    ///  [2] Autocorrelation - Wikipedia, the free encyclopedia,
    ///  http://en.wikipedia.org/wiki/Autocorrelation
    ///  [3] Tolonen T., and Karjalainen, M. (2000). A computationally efficient multipitch analysis model.
    ///  IEEE Transactions on Audio, Speech, and Language Processing, 8(6), 708-716.
    /// </summary>
    public class AutoCorrelation
    {
        /// <summary>
        /// Bool indicating whether or not the output should be normalized.
        /// </summary>
        private bool unbiasedNormalization = false;

        /// <summary>
        /// Bool indicating what operations to take on the intermediary FFT before computing the inverse. 
        /// </summary>
        private bool generalized = false;

        /// <summary>
        /// How much compression should be done on the frequency domain.
        /// This float corresponds to the exponent in a Math.Pow() call when generalized == true
        /// </summary>
        private float frequencyDomainCompression = 0.5f;

        /// <summary>
        /// Creates a new object that can compute the autocorrelation of a signal.
        /// </summary>
        /// <param name="normalize">
        /// Whether or not the output should be normalized and unbiased.
        /// </param>
        /// <param name="generalized">
        /// Determines which operation to apply to intermediary FFT.
        /// </param>
        /// <param name="frequencyDomainCompression">
        /// How much compression should be done on the frequency domain.
        /// This float corresponds to the exponent in a Math.Pow() call when generalized == true
        /// </param>
        public AutoCorrelation(NormalizeType normalize = NormalizeType.Standard, bool generalized = false, float frequencyDomainCompression = 0.5f)
        {
            this.unbiasedNormalization = normalize == NormalizeType.Unbiased;
            this.generalized = generalized;
            this.frequencyDomainCompression = frequencyDomainCompression;
        }

        /// <summary>
        /// Computes the autocorrelation of the signal.
        /// https://en.wikipedia.org/wiki/Autocorrelation
        /// </summary>
        /// <param name="signal">
        /// The signal.
        /// </param>
        /// <returns>
        /// The correlation array.
        /// </returns>
        public float[] Compute(float[] signal)
        {
            if (signal == null || signal.Length == 0)
            {
                return new float[0];
            }

            // formula to get the auto-correlation (in matlab) is:
            //  [M,N] = size(x)
            //  X = fft(x,2^nextpow2(2*M-1));
            //  c = ifft(abs(X).^2);
            int size = signal.Length;

            Complex32[] fft = Spectrum.ComputeFFT(signal);
            int sizeFFT = fft.Length;
            for(int i = 0; i < fft.Length; i++)
            {
                if (!this.generalized)
                {
                    // Real * Real + Image * Image = multiplying a complex number by its complex conjugate (a + bi)(a - bi) => a^2 + b^2
                    fft[i] = new Complex32((fft[i].Real * fft[i].Real) + (fft[i].Imaginary * fft[i].Imaginary), 0);
                }
                else
                {
                    fft[i] = new Complex32((float)Math.Pow(Math.Sqrt(Math.Pow(fft[i].Real / sizeFFT, 2) + Math.Pow(fft[i].Imaginary / sizeFFT, 2)), this.frequencyDomainCompression), 0);
                }
            }

            float[] corr = Spectrum.ComputeIFFT(fft, !this.generalized);
            if (this.unbiasedNormalization)
            {
                for(int i = 0; i < size; i++)
                {
                    corr[i] = corr[i] / (size - i);
                }
            }

            return corr;
        }
    }

    /// <summary>
    /// Enum used to identify the normalizaiton type used by the AutoCorrelation class.
    /// </summary>
    public enum NormalizeType
    {
        Standard,
        Unbiased
    }
}
