namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PitchSalienceFunction
    {
        /// <summary>
        /// salience function bin resolution [cents]
        /// </summary>
        float binResolution;

        /// <summary>
        /// the reference frequency for Hertz to cent convertion [Hz], corresponding to the 0th cent bin
        /// </summary>
        float referenceFrequency;

        /// <summary>
        /// peak magnitude threshold (maximum allowed difference from the highest peak in dBs)
        /// </summary>
        float magnitudeThreshold;

        /// <summary>
        /// magnitude compression parameter (=0 for maximum compression, =1 for no compression)
        /// </summary>
        float magnitudeCompression;

        /// <summary>
        /// number of considered harmonics
        /// </summary>
        int numberHarmonics;

        /// <summary>
        /// harmonic weighting parameter (weight decay ratio between two consequent harmonics, =1 for no decay)
        /// </summary>
        float harmonicWeight;

        /// <summary>
        /// precomputed vector of weights for n-th harmonics
        /// </summary>
        List<float> harmonicWeights;

        /// <summary>
        /// precomputed vector of weights for salience propagation to nearest bins
        /// </summary>
        List<float> nearestBinsWeights;

        /// <summary>
        /// 
        /// </summary>
        int numberBins;

        /// <summary>
        /// number of bins in a semitone
        /// </summary>
        int binsInSemitone;

        /// <summary>
        /// number of bins in an octave
        /// </summary>
        float binsInOctave;

        /// <summary>
        /// precomputed addition term used for Hz to cent bin conversion
        /// </summary>
        float referenceTerm;

        /// <summary>
        /// fraction of maximum magnitude in frame corresponding to _magnitudeCompression difference in dBs
        /// </summary>
        float magnitudeThresholdLinear;

        /// <summary>
        ///  This algorithm computes the pitch salience function of a signal frame given its spectral peaks. 
        ///  The salience function covers a pitch range of nearly five octaves (i.e., 6000 cents), starting from the \"referenceFrequency\", and is quantized into cent bins according to the specified \"binResolution\". 
        ///  The salience of a given frequency is computed as the sum of the weighted energies found at integer multiples (harmonics) of that frequency. 
        ///
        ///  This algorithm is intended to receive its \"frequencies\" and \"magnitudes\" inputs from the SpectralPeaks algorithm. 
        ///  The output is a vector of salience values computed for the cent bins. The 0th bin corresponds to the specified \"referenceFrequency\".
        ///
        ///  When input vectors differ in size or are empty, an exception is thrown. 
        ///  Input vectors must contain positive frequencies and not contain negative magnitudes otherwise an exception is thrown. 
        ///  It is highly recommended to avoid erroneous peak duplicates (peaks of the same frequency occuring more than ones), but it is up to the user's own control and no exception will be thrown.
        ///
        ///  References:
        ///    [1] J. Salamon and E. Gómez, \"Melody extraction from polyphonic music
        ///    signals using pitch contour characteristics,\" IEEE Transactions on Audio,
        ///    Speech, and Language Processing, vol. 20, no. 6, pp. 1759–1770, 2012.
        /// </summary>
        /// <param name="binResolution">
        /// salience function bin resolution [cents]
        /// </param>
        /// <param name="referenceFrequency">
        /// the reference frequency for Hertz to cent convertion [Hz], corresponding to the 0th cent bin
        /// </param>
        /// <param name="magnitudeThreshold">
        /// peak magnitude threshold (maximum allowed difference from the highest peak in dBs)
        /// </param>
        /// <param name="magnitudeCompression">
        /// magnitude compression parameter (=0 for maximum compression, =1 for no compression)
        /// </param>
        /// <param name="numberHarmonics">
        /// number of considered harmonics
        /// </param>
        /// <param name="harmonicWeight">
        /// harmonic weighting parameter (weight decay ratio between two consequent harmonics, =1 for no decay)
        /// </param>
        public PitchSalienceFunction(
            float binResolution = 10, 
            float referenceFrequency = 55, 
            float magnitudeThreshold = 40, 
            float magnitudeCompression = 1, 
            int numberHarmonics = 20, 
            float harmonicWeight = 0.8f)
        {
            this.referenceFrequency = referenceFrequency;
            this.binResolution = binResolution;
            this.magnitudeThreshold = magnitudeThreshold;
            this.magnitudeCompression = magnitudeCompression;
            this.numberHarmonics = numberHarmonics;
            this.harmonicWeight = harmonicWeight;

            this.numberBins = (int)Math.Floor(6000f / this.binResolution);
            this.binsInSemitone = (int)Math.Floor(100f / this.binResolution);
            this.binsInOctave = 1200f / this.binResolution;
            this.referenceTerm = 0.5f - this.binsInOctave * MathHelpers.Log2(this.referenceFrequency);
            this.magnitudeThresholdLinear = (float)(1f / Math.Pow(10, this.magnitudeThreshold / 20.0));

            this.harmonicWeights = new List<float>(this.numberHarmonics);
            for(int h = 0; h < this.numberHarmonics; h++)
            {
                this.harmonicWeights.Add((float)Math.Pow(this.harmonicWeight, h));
            }

            this.nearestBinsWeights = new List<float>(this.binsInSemitone + 1);
            for(int b = 0; b <= this.binsInSemitone; b++)
            {
                this.nearestBinsWeights.Add((float)Math.Pow(Math.Cos((b / this.binsInSemitone) * Math.PI / 2.0), 2));
            }
        }

        /// <summary>
        /// Compute the salience function.
        /// </summary>
        /// <param name="frequencies">
        /// the frequencies of the spectral peaks [Hz]
        /// </param>
        /// <param name="magnitudes">
        /// the magnitudes of the spectral peaks
        /// </param>
        /// <returns>
        /// array of the quantized pitch salience values
        /// </returns>
        public float[] Compute(float[] frequencies, float[] magnitudes)
        {
            // validate the inputs
            if (magnitudes?.Length != frequencies?.Length)
            {
                throw new Exception($"PitchSalienceFunction: frequency and magnitude input vectors must have the same size.");
            }

            if (frequencies == null || frequencies.Length == 0)
            {
                return new float[this.numberBins];
            }

            int numberPeaks = frequencies.Length;
            for (int i = 0; i < numberPeaks; i++)
            {
                if (frequencies[i] <= 0)
                {
                    throw new Exception("PitchSalienceFunction: spectral peak frequencies must be positive");
                }
                if (magnitudes[i] <= 0)
                {
                    throw new Exception("PitchSalienceFunction: spectral peak magnitudes must be positive");
                }
            }

            float[] salienceFunction = new float[this.numberBins];
            float minMagnitude = magnitudes.Max() * this.magnitudeThresholdLinear;
            for (int i = 0; i < numberPeaks; i++)
            {
                // remove peaks with low magnitudes:
                // 20 * log10(magnitudes[argmax(magnitudes)]/magnitudes[i]) >= _magnitudeThreshold
                if (magnitudes[i] <= minMagnitude)
                {
                    continue;
                }
                float magnitudeFactor = (float)Math.Pow(magnitudes[i], this.magnitudeCompression);

                // find all bins where this peak contributes salience
                // these bins are (sub)harmonics of the peak frequency
                // propagate salience to nearest bins within +- one semitone

                for (int h = 0; h < this.numberHarmonics; h++)
                {
                    int h_bin = this.FrequencyToCentBin(frequencies[i] / (h + 1));
                    if (h_bin < 0)
                    {
                        break;
                    }

                    for (int b = Math.Max(0, h_bin - this.binsInSemitone); b <= Math.Min(this.numberBins - 1, h_bin + this.binsInSemitone); b++)
                    {
                        salienceFunction[b] += magnitudeFactor * this.nearestBinsWeights[Math.Abs(b - h_bin)] * this.harmonicWeights[h];
                    }
                }
            }

            return salienceFunction;
        }

        /// <summary>
        ///  +0.5 term is used instead of +1 (as in [1]) to center 0th bin to 55Hz
        ///  formula: floor(1200 * log2(frequency / _referenceFrequency) / _binResolution + 0.5)
        ///     --> 1200 * (log2(frequency) - log2(_referenceFrequency)) / _binResolution + 0.5
        ///     --> 1200 * log2(frequency) / _binResolution + (0.5 - 1200 * log2(_referenceFrequency) / _binResolution)
        /// </summary>
        /// <param name="frequency">
        /// Input frequency
        /// </param>
        /// <returns>
        /// Corresponding cent bin.
        /// </returns>
        private int FrequencyToCentBin(float frequency)
        {

            return (int)Math.Floor(this.binsInOctave * MathHelpers.Log2(frequency) + this.referenceTerm);
        }
    }
}
