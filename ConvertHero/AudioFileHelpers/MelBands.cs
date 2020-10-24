namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Supported Mel Warping Functions
    /// </summary>
    public enum MelWarpingFunctions
    {
        HertzToMel10,
        HertzToMel
    }

    /// <summary>
    /// Supported Mel Weighting Functions
    /// </summary>
    public enum MelWeightingFunctions
    {
        Warping,
        Linear
    }

    /// <summary>
    /// Class allows the conversion of a standard spectrum to a Mel spectrum
    /// </summary>
    public class MelBands
    {
        /// <summary>
        /// The size of the input spectrum.
        /// </summary>
        private int spectrumSize;

        /// <summary>
        /// The number of mel bands to output.
        /// </summary>
        private int bandsToOutput;

        /// <summary>
        /// The sample rate of the spectrum.
        /// </summary>
        private int sampleRate;

        /// <summary>
        /// The lower frequency bound.
        /// </summary>
        private float lowFrequencyBound;

        /// <summary>
        /// The high frequency bound.
        /// </summary>
        private float highFrequencyBound;

        /// <summary>
        /// Whether or not to normalize to a unit sum
        /// </summary>
        private bool unitSum;

        /// <summary>
        /// Whether or not to log the result
        /// </summary>
        private bool log;

        /// <summary>
        /// What units the result should be in.
        /// </summary>
        private OutputUnits type;

        /// <summary>
        /// The function used to warp
        /// </summary>
        private Func<float, float> warpingFunction;

        /// <summary>
        /// The function used to undo the warp.
        /// </summary>
        private Func<float, float> inverseWarpingFunction;

        /// <summary>
        /// The function used to weight.
        /// </summary>
        private Func<float, float> weightingFunction;

        /// <summary>
        /// The filter frequencies.
        /// </summary>
        private float[] filterFrequencies;

        /// <summary>
        /// The triangular bands used to bin the spectrum.
        /// </summary>
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
            this.type = type;
            this.SetWarpingFunctions(warping, weighting);
            this.CalculateFilterFrequencies();

            if (spectrumSize > 0)
            {
                this.triangleBands = new TriangularBands(this.filterFrequencies, this.sampleRate, this.spectrumSize, this.log, type, unitSum, this.weightingFunction);
            }
        }

        /// <summary>
        /// Compute the mel-bands for the spectrum.
        /// </summary>
        /// <param name="spectrum">
        /// The input spectrum.
        /// </param>
        /// <returns>
        /// The mel banded spectrum.
        /// </returns>
        public float[] Compute(float[] spectrum)
        {
            if (this.triangleBands == null || this.triangleBands.InputSize != spectrum.Length)
            {
                this.triangleBands = new TriangularBands(this.filterFrequencies, this.sampleRate, spectrum.Length, this.log, this.type, this.unitSum, this.weightingFunction);
            }

            return this.triangleBands.ComputeTriangleBands(spectrum);
        }

        /// <summary>
        /// Pre-compute the filter frequencies.
        /// </summary>
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

        /// <summary>
        /// Set the warping/weighting functions depending on user provided properties.
        /// </summary>
        /// <param name="warping">
        /// The type of warping.
        /// </param>
        /// <param name="weighting">
        /// The type of weighting.
        /// </param>
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
