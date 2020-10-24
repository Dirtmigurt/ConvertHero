namespace ConvertHero.AudioFileHelpers
{
    using MathNet.Numerics;
    using System;

    /// <summary>
    /// This algorithm computes energies/magnitudes in ERB bands of a spectrum. 
    /// The Equivalent Rectangular Bandwidth (ERB) scale is used. 
    /// The algorithm applies a frequency domain filterbank using gammatone filters. 
    /// Adapted from matlab code in:  D. P. W. Ellis (2009). 'Gammatone-like spectrograms', web resource [1].
    /// 
    /// References:
    ///   [1] http://www.ee.columbia.edu/~dpwe/resources/matlab/gammatonegram/
    ///   [2] B. C. Moore and B. R. Glasberg, "Suggested formulae for calculating auditory-filter bandwidths and excitation patterns, 
    ///   Journal of the Acoustical Society of America, vol. 74, no. 3, pp. 750–753, 1983.
    /// </summary>
    public class ERBBands
    {
        /// <summary>
        /// EarQ constant
        /// </summary>
        private const float EarQ = 9.26449f;

        /// <summary>
        /// MinBandwidth constant
        /// </summary>
        private const float MinBW = 24.7f;

        /// <summary>
        /// The size of the input spectrum.
        /// </summary>
        private int inputSize;

        /// <summary>
        /// The number of ERBBands.
        /// </summary>
        private int numberBands;

        /// <summary>
        /// The sample rate of the input signal.
        /// </summary>
        private float sampleRate;

        /// <summary>
        /// The lower frequency bound.
        /// </summary>
        private float lowFrequencyBound;

        /// <summary>
        /// The high frequency bound.
        /// </summary>
        private float highFrequencyBound;

        /// <summary>
        /// The width of the ERB Band.
        /// </summary>
        private float width;

        /// <summary>
        /// The unit type of the input spectrum (Power/Magnitude)
        /// </summary>
        private SpectrumType type;

        /// <summary>
        /// The frequencies in the filters.
        /// </summary>
        private float[] filterFrequencies;

        /// <summary>
        /// FilterSize x SpectrumSize Array of filter coefficients
        /// </summary>
        private float[,] filterCoefficients = null;

        /// <summary>
        /// Create new ERBBands filter.
        /// </summary>
        /// <param name="inputSize">
        /// The size of the input spectrum
        /// </param>
        /// <param name="numberBands">
        /// The number of output bands
        /// </param>
        /// <param name="sampleRate">
        /// The sampling rate of the audio signal [Hz]
        /// </param>
        /// <param name="lowFrequencyBound">
        /// A lower-bound limit for the frequencies to be included in the bands
        /// </param>
        /// <param name="highFrequencyBound">
        /// An upper-bound limit for the frequencies to be included in the bands
        /// </param>
        /// <param name="width">
        /// Filter width with respect to ERB
        /// </param>
        /// <param name="type">
        /// Use magnitude or power spectrum
        /// </param>
        public ERBBands(int inputSize = 1025, int numberBands = 40, float sampleRate = 44100, float lowFrequencyBound = 50, float highFrequencyBound = 22050, float width = 1, SpectrumType type = SpectrumType.Power)
        {
            if (highFrequencyBound > sampleRate * 0.5)
            {
                throw new Exception("ERBBands: High frequency bound cannot be higher than Nyquist Frequency.");
            }

            if (highFrequencyBound < lowFrequencyBound)
            {
                throw new Exception("ERBBands: High frequency bound cannot be lower than low frequency bound.");
            }

            this.inputSize = inputSize;
            this.numberBands = numberBands;
            this.sampleRate = sampleRate;
            this.lowFrequencyBound = lowFrequencyBound;
            this.highFrequencyBound = highFrequencyBound;
            this.width = width;
            this.type = type;

            // Calculate filter freqeuncies
            this.CalculateFilterFrequencies();

            // Create Filters
            this.CreateFilters(this.inputSize);
        }

        /// <summary>
        /// Precompute the filter frequencies.
        /// </summary>
        private void CalculateFilterFrequencies()
        {
            int filterSize = this.numberBands;
            this.filterFrequencies = new float[filterSize];
            float filterSizeInv = 1f / filterSize;
            float bandwidth = EarQ * MinBW;

            for(int i = 1; i < filterSize+1; i++)
            {
                this.filterFrequencies[filterSize-i] = (float)(-bandwidth + Math.Exp(i * (-Math.Log(this.highFrequencyBound + bandwidth) + Math.Log(this.lowFrequencyBound + bandwidth)) * filterSizeInv) * (this.highFrequencyBound + bandwidth));
            }
        }

        /// <summary>
        /// Just a bunch of magic numbers.
        /// </summary>
        /// <param name="spectrumSize"></param>
        private void CreateFilters(int spectrumSize)
        {
            if (spectrumSize < 2)
            {
                throw new Exception("ERBBands: Filter bank cannot be computed from a spectrum with less than 2 bins.");
            }

            int filterSize = this.numberBands;
            Complex32[] ucirc = new Complex32[spectrumSize];
            Complex32 oneJ = new Complex32(0, 1);
            float order = 1;
            this.filterCoefficients = new float[filterSize, spectrumSize];
            float fftSize = (spectrumSize - 1) * 2;
            for(int i = 0; i < spectrumSize; i++)
            {
                // e^(i(2*pi*i/fftsize)) = cos(2*pi*i/fftsize) + i*sin(2*pi*i/fftsize)
                float theta = (float)(2 * Math.PI * i / fftSize);
                ucirc[i] = new Complex32((float)Math.Cos(theta), (float)Math.Sin(theta));
            }

            float sqrP = (float)Math.Sqrt(3 + Math.Pow(2, 1.5));
            float sqrM = (float)Math.Sqrt(3 - Math.Pow(2, 1.5));

            for(int i = 0; i < filterSize; i++)
            {
                float cf = this.filterFrequencies[i];
                float ERB = (float)(this.width * Math.Pow(Math.Pow(cf/EarQ, order) + Math.Pow(MinBW, order), 1f / order));
                float B = (float)(1.019 * 2 * Math.PI * ERB);
                float r = (float)Math.Exp(-B / this.sampleRate);
                float theta = (float)(2 * Math.PI * cf / this.sampleRate);
                Complex32 pole = r * new Complex32((float)Math.Cos(theta), (float)Math.Sin(theta));
                float T = 1f / this.sampleRate;
                float GTord = 4;

                float sinCf = (float)Math.Sin(2 * cf * Math.PI * T);
                float cosCf = (float)Math.Cos(2 * cf * Math.PI * T);
                float gtCos = (float)(2 * T * cosCf / Math.Exp(B * T));
                float gtSin = (float)(T * sinCf / Math.Exp(B * T));

                float A11 = -(gtCos + 2 * sqrP * gtSin) / 2;
                float A12 = -(gtCos - 2 * sqrP * gtSin) / 2;
                float A13 = -(gtCos + 2 * sqrM * gtSin) / 2;
                float A14 = -(gtCos - 2 * sqrM * gtSin) / 2;

                float[] zeros = new float[4];
                zeros[0] = -A11 / T;
                zeros[1] = -A12 / T;
                zeros[2] = -A13 / T;
                zeros[3] = -A14 / T;

                Complex32 g1 = -2 * Complex32.Exp(4 * oneJ * cf * (float)Math.PI * T);
                Complex32 g2 =  2 * T * Complex32.Exp(-(B*T) + 2 * oneJ * cf * (float)Math.PI * T);
                Complex32 cxExp = Complex32.Exp(4 * oneJ * cf * (float)Math.PI * T);
                float filterGain = (float)Complex32.Abs(
                  (g1 + g2 * (cosCf - sqrM * sinCf)) *
                  (g1 + g2 * (cosCf + sqrM * sinCf)) *
                  (g1 + g2 * (cosCf - sqrP * sinCf)) *
                  (g1 + g2 * (cosCf + sqrP * sinCf)) /
                  Complex32.Pow((-2 / Complex32.Exp(2 * B * T) -
                    2 * cxExp + 2 * (1 + cxExp) / Complex32.Exp(B * T)), 4));

                for(int j = 0; j < spectrumSize; j++)
                {
                    this.filterCoefficients[i, j] = (float)((Math.Pow(T, 4) / filterGain) *
                        Complex32.Abs(ucirc[j] - zeros[0]) * Complex32.Abs(ucirc[j] - zeros[1]) *
                        Complex32.Abs(ucirc[j] - zeros[2]) * Complex32.Abs(ucirc[j] - zeros[3]) *
                        Math.Pow(Complex32.Abs((pole - ucirc[j]) * (pole - ucirc[j])), -GTord));
                }
            }
        }

        /// <summary>
        /// Actually perform the work on the input spectrum.
        /// </summary>
        /// <param name="spectrum">
        /// The audio spectrum.
        /// </param>
        /// <returns>
        /// The energies/magnitudes of each band.
        /// </returns>
        public float[] Compute(float[] spectrum)
        {
            int filterSize = this.numberBands;
            int spectrumSize = spectrum.Length;

            if (this.filterCoefficients == null || this.filterCoefficients.GetLength(1) != spectrumSize)
            {
                this.CreateFilters(spectrumSize);
            }

            float[] bands = new float[filterSize];
            if (this.type == SpectrumType.Magnitude)
            {
                for(int i = 0; i < filterSize; i++)
                {
                    for(int j = 0; j < spectrumSize; j++)
                    {
                        bands[i] += (spectrum[j]) * this.filterCoefficients[i, j];
                    }
                }
            }
            else if(this.type == SpectrumType.Power)
            {
                for (int i = 0; i < filterSize; i++)
                {
                    for (int j = 0; j < spectrumSize; j++)
                    {
                        bands[i] += (spectrum[j] * spectrum[j]) * this.filterCoefficients[i, j];
                    }
                }
            }

            return bands;
        }
    }

    /// <summary>
    /// The unit type of the input spectrum.
    /// </summary>
    public enum SpectrumType
    {
        Magnitude,
        Power
    }
}
