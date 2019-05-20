using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public enum OutputUnits
    {
        Magnitude,
        Power
    }

    public class TriangularBands
    {
        private int sampleRate = -1;

        private float[,] filterBank = null;

        private float[] bandFreqeuncies = null;

        private Func<float, float> weightingFunction;

        private bool normalizeUnitSum = false;

        private OutputUnits type;

        private bool isLog = false;

        public int InputSize = -1;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frequencyBands"></param>
        /// <param name="sampleRate"></param>
        /// <param name="inputSize"></param>
        /// <param name="weightingFunction">
        /// Default Weighting function = MathHelpers.HertzToHertz
        /// </param>
        public TriangularBands(float[] frequencyBands, int sampleRate, int inputSize, bool log = false, OutputUnits type = OutputUnits.Power, bool normalizeUnitSum = true, Func<float, float> weightingFunction = null)
        {
            this.sampleRate = sampleRate;
            this.bandFreqeuncies = frequencyBands;
            this.weightingFunction = weightingFunction ?? MathHelpers.HertzToHertz;
            this.normalizeUnitSum = normalizeUnitSum;
            this.type = type;
            this.isLog = log;
            if (inputSize > 2)
            {
                this.CreateFilterBank(inputSize);
            }
        }

        public float[] ComputeTriangleBands(float[] spectrum)
        {
            if (this.filterBank == null || filterBank.GetLength(1) != spectrum.Length)
            {
                this.CreateFilterBank(spectrum.Length);
            }

            float frequencyScale = (this.sampleRate / 2.0f) / (spectrum.Length - 1);
            float[] bands = new float[this.bandFreqeuncies.Length - 2];
            for (int i = 0; i < bands.Length; ++i)
            {

                // Find margins for FFT bins to iterate through
                // (all bins fall inside the triangle and therefore have non-zero weights).
                int jbegin = (int)Math.Ceiling(this.bandFreqeuncies[i] / frequencyScale);
                int jend = (int)Math.Floor(this.bandFreqeuncies[i + 2] / frequencyScale);

                for (int j = jbegin; j <= jend; ++j)
                {

                    if (this.type == OutputUnits.Power)
                    {
                        bands[i] += (spectrum[j] * spectrum[j]) * this.filterBank[i, j];
                    }

                    if (this.type == OutputUnits.Magnitude)
                    {
                        bands[i] += (spectrum[j]) * this.filterBank[i, j];
                    }

                }
                if (this.isLog)
                {
                    // Log base 2 of the band value
                    bands[i] = (float)(Math.Log(1 + bands[i]) / Math.Log(2));
                }
            }

            return bands;
        }

        private void CreateFilterBank(int inputSize)
        {
            this.InputSize = inputSize;
            this.filterBank = new float[this.bandFreqeuncies.Length - 2, inputSize];

            float frequencyScale = (this.sampleRate / 2.0f) / (inputSize - 1);
            for (int bandIndex = 0; bandIndex < this.bandFreqeuncies.Length - 2; bandIndex++)
            {
                float fstep1 = this.weightingFunction(this.bandFreqeuncies[bandIndex + 1]) - this.weightingFunction(this.bandFreqeuncies[bandIndex]);
                float fstep2 = this.weightingFunction(this.bandFreqeuncies[bandIndex + 2]) - this.weightingFunction(this.bandFreqeuncies[bandIndex + 1]);

                // Find margins for FFT bins to iterate through
                // (all bins fall inside the triangle).
                int jbegin = (int)Math.Ceiling(this.bandFreqeuncies[bandIndex] / frequencyScale);
                int jend = (int)Math.Floor(this.bandFreqeuncies[bandIndex + 2] / frequencyScale);

                if (jend >= inputSize)
                {
                    throw new Exception($"TriangularBands: the 'frequencyBands' parameter contains a value above the Nyquist frequency ({sampleRate / 2} Hz): {this.bandFreqeuncies.Last()}");
                }

                float weight = 0f;
                for (int j = jbegin; j <= jend; ++j)
                {
                    float binfreq = j * frequencyScale;
                    // in the ascending part of the triangle...
                    if (binfreq < this.bandFreqeuncies[bandIndex + 1])
                    {
                        this.filterBank[bandIndex, j] = (this.weightingFunction(binfreq) - this.weightingFunction(this.bandFreqeuncies[bandIndex])) / fstep1;
                    }
                    // in the descending part of the triangle...
                    else if (binfreq >= this.bandFreqeuncies[bandIndex + 1])
                    {
                        this.filterBank[bandIndex, j] = (this.weightingFunction(this.bandFreqeuncies[bandIndex + 2]) - this.weightingFunction(binfreq)) / fstep2;
                    }
                    weight += this.filterBank[bandIndex, j];
                }

                if (weight <= 1e-4)
                {
                    throw new Exception("TriangularBands: the number of spectrum bins is insufficient for the specified number of triangular bands. Use zero padding to increase the number of FFT bins.");
                }

                // normalize the filter weights
                if (normalizeUnitSum)
                {
                    for (int j = jbegin; j <= jend; ++j)
                    {
                        this.filterBank[bandIndex, j] = this.filterBank[bandIndex, j] / weight;
                    }
                }
            }
        }
    }
}
