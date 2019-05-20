using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public enum MelWarpingFunctions
    {
        HertzToMel10,
        HertzToMel
    }

    public enum MelWeightingFunctions
    {
        Warping,
        Linear
    }

    public class MelBands
    {
        private int spectrumSize;
        private int bandsToOutput;
        private int sampleRate;
        private float lowFrequencyBound;
        private float highFrequencyBound;
        private bool unitSum;
        private bool log;
        private OutputUnits type;
        private Func<float, float> warpingFunction;
        private Func<float, float> inverseWarpingFunction;
        private Func<float, float> weightingFunction;
        private float[] filterFrequencies;
        TriangularBands triangleBands;

        /// <summary>
        /// Compute the mel spectrogram from an input spectrum.
        /// </summary>
        /// <param name="spectrumSize">
        /// Number of bins in the input spectrum.
        /// </param>
        /// <param name="bandsToOutput">
        /// Number of output bands.
        /// </param>
        /// <param name="sampleRate">
        /// Sample rate of the source signal.
        /// </param>
        /// <param name="lowFrequencyBound">
        /// The lower-bound limit for the frequencies that will be included in the output spectrum.
        /// </param>
        /// <param name="highFrequencyBound">
        /// The upper-bound limit for the frequencies that will be included in the output spectrum.
        /// </param>
        /// <param name="warping">
        /// The scale implementation type.
        /// </param>
        /// <param name="weighting">
        /// Type of weighting functino for determining triangle area.
        /// </param>
        /// <param name="unitSum">
        /// True makes the area of all triangles equal to 1, False makes the vertex of all triangles equal to 1.
        /// </param>
        /// <param name="type">
        /// Specifies the units of the output spectrum. Power is the input units squared, Magnitude keeps it unchanged.
        /// </param>
        /// <param name="log">
        /// Compute the log-energies (log10 (1+energy)) of each band.
        /// </param>
        public MelBands(
            int bandsToOutput, 
            int sampleRate,
            int spectrumSize = -1,
            float lowFrequencyBound = 40.0f, 
            float highFrequencyBound = 4000, 
            MelWarpingFunctions warping = MelWarpingFunctions.HertzToMel, 
            MelWeightingFunctions weighting = MelWeightingFunctions.Linear, 
            bool unitSum = false, 
            OutputUnits type = OutputUnits.Power, 
            bool log = false)
        {
            this.spectrumSize = spectrumSize;
            this.bandsToOutput = bandsToOutput;
            this.sampleRate = sampleRate;
            this.lowFrequencyBound = lowFrequencyBound;
            this.highFrequencyBound = highFrequencyBound;
            this.unitSum = unitSum;
            this.log = log;
            this.SetWarpingFunctions(warping, weighting);
            this.CalculateFilterFrequencies();

            if (spectrumSize > 0)
            {
                this.triangleBands = new TriangularBands(this.filterFrequencies, this.sampleRate, this.spectrumSize, this.log, type, unitSum, this.weightingFunction);
            }
        }

        public float[] Compute(float[] spectrum)
        {
            if (this.triangleBands == null || this.triangleBands.InputSize != spectrum.Length)
            {
                this.triangleBands = new TriangularBands(this.filterFrequencies, this.sampleRate, spectrum.Length, this.log, this.type, this.unitSum, this.weightingFunction);
            }

            return this.triangleBands.ComputeTriangleBands(spectrum);
        }

        private void CalculateFilterFrequencies()
        {
            int filterSize = this.bandsToOutput;
            this.filterFrequencies = new float[filterSize + 2];

            float lowMelFrequencyBound = this.warpingFunction(this.lowFrequencyBound);
            float highMelFrequencyBound = this.warpingFunction(this.highFrequencyBound);
            float melFrequencyIncrement = (highMelFrequencyBound - lowMelFrequencyBound) / (filterSize + 1);

            float melFrequency = lowMelFrequencyBound;
            for (int i = 0; i < filterSize + 2; i++)
            {
                this.filterFrequencies[i] = this.inverseWarpingFunction(melFrequency);
                melFrequency += melFrequencyIncrement;
            }
        }

        private void SetWarpingFunctions(MelWarpingFunctions warping, MelWeightingFunctions weighting)
        {
            if (warping == MelWarpingFunctions.HertzToMel10)
            {
                this.warpingFunction = MathHelpers.HertzToMel10;
                this.inverseWarpingFunction = MathHelpers.Mel10ToHertz;
            }
            else if (warping == MelWarpingFunctions.HertzToMel)
            {
                this.warpingFunction = MathHelpers.HertzToMel;
                this.inverseWarpingFunction = MathHelpers.MelToHertz;
            }

            if (weighting == MelWeightingFunctions.Warping)
            {
                this.weightingFunction = this.warpingFunction;
            }
            else if (weighting == MelWeightingFunctions.Linear)
            {
                this.weightingFunction = MathHelpers.HertzToHertz;
            }
        }
    }
}
