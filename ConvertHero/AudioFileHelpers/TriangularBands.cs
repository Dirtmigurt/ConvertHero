namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Linq;

    /// <summary>
    /// Supported output units.
    /// </summary>
    public enum OutputUnits
    {
        Magnitude,
        Power
    }

    /// <summary>
    /// 
    /// </summary>
    public class TriangularBands
    {
        /// <summary>
        /// The sample rate of the audio signal
        /// </summary>
        private int sampleRate = -1;

        /// <summary>
        /// The triangle band filter bank
        /// </summary>
        private float[,] filterBank = null;

        /// <summary>
        /// The band frequencies
        /// </summary>
        private float[] bandFreqeuncies = null;

        /// <summary>
        /// the weighting function.
        /// </summary>
        private Func<float, float> weightingFunction;

        /// <summary>
        /// Whether or not to normalize to unit sum.
        /// </summary>
        private bool normalizeUnitSum = false;

        /// <summary>
        /// the unit type of the output.
        /// </summary>
        private OutputUnits type;

        /// <summary>
        /// should the output energies be logged?
        /// </summary>
        private bool isLog = false;

        /// <summary>
        /// 
        /// </summary>
        public int InputSize = -1;

        /// <summary>
        /// </summary>
        /// <param name="sampleRate">the sampling rate of the audio signal [Hz]</param>
        /// <param name="inputSize">the size of the spectrum</param>
        /// <param name="weightingFunction">
        /// Default Weighting function = MathHelpers.HertzToHertz
        /// </param>
        /// <param name="frequencyBands">
        /// list of frequency ranges into which the spectrum is divided (these must be in ascending order and connot contain duplicates)
        /// each triangle is built as x(i-1)=0, x(i)=1, x(i+1)=0 over i, the resulting number of bands is size of input array - 2
        /// </param>
        /// <param name="log">compute log-energies (log10 (1 + energy))</param>
        /// <param name="type">use magnitude or power spectrum</param>
        /// <param name="normalizeUnitSum">
        /// Whether or not to make each triangle area equal to 1 summing the actual weights of spectrum bins,
        /// </param>
        /// <param name="weightingFunction">type of weighting function for determining triangle area</param>
        public TriangularBands(float[] frequencyBands, int sampleRate, int inputSize, bool log = false, OutputUnits type = OutputUnits.Power, bool normalizeUnitSum = true, Func<float, float> weightingFunction = null)
        {
            this.sampleRate = sampleRate;
            this.bandFreqeuncies = frequencyBands;
            // superfluxBands = new double[] {21.533203125, 43.06640625, 64.599609375, 86.1328125, 107.666015625, 129.19921875, 150.732421875, 172.265625, 193.798828125, 215.33203125, 236.865234375, 258.3984375, 279.931640625, 301.46484375, 322.998046875, 344.53125, 366.064453125, 387.59765625, 409.130859375, 430.6640625, 452.197265625, 473.73046875, 495.263671875, 516.796875, 538.330078125, 559.86328125, 581.396484375, 602.9296875, 624.462890625, 645.99609375, 667.529296875, 689.0625, 710.595703125, 732.12890625, 753.662109375, 775.1953125, 796.728515625, 839.794921875, 861.328125, 882.861328125, 904.39453125, 925.927734375, 968.994140625, 990.52734375, 1012.060546875, 1055.126953125, 1076.66015625, 1098.193359375, 1141.259765625, 1184.326171875, 1205.859375, 1248.92578125, 1270.458984375, 1313.525390625, 1356.591796875, 1399.658203125, 1442.724609375, 1485.791015625, 1528.857421875, 1571.923828125, 1614.990234375, 1658.056640625, 1701.123046875, 1765.72265625, 1808.7890625, 1873.388671875, 1916.455078125, 1981.0546875, 2024.12109375, 2088.720703125, 2153.3203125, 2217.919921875, 2282.51953125, 2347.119140625, 2411.71875, 2497.8515625, 2562.451171875, 2627.05078125, 2713.18359375, 2799.31640625, 2885.44921875, 2950.048828125, 3036.181640625, 3143.84765625, 3229.98046875, 3316.11328125, 3423.779296875, 3509.912109375, 3617.578125, 3725.244140625, 3832.91015625, 3940.576171875, 4069.775390625, 4177.44140625, 4306.640625, 4435.83984375, 4565.0390625, 4694.23828125, 4844.970703125, 4974.169921875, 5124.90234375, 5275.634765625, 5426.3671875, 5577.099609375, 5749.365234375, 5921.630859375, 6093.896484375, 6266.162109375, 6459.9609375, 6653.759765625, 6847.55859375, 7041.357421875, 7256.689453125, 7450.48828125, 7687.353515625, 7902.685546875, 8139.55078125, 8376.416015625, 8613.28125, 8871.6796875, 9130.078125, 9388.4765625, 9668.408203125, 9948.33984375, 10249.8046875, 10551.26953125, 10852.734375, 11175.732421875, 11498.73046875, 11843.26171875, 12187.79296875, 12553.857421875, 12919.921875, 13285.986328125, 13673.583984375, 14082.71484375, 14491.845703125, 14922.509765625, 15353.173828125, 15805.37109375, 16257.568359375};
            this.weightingFunction = weightingFunction ?? MathHelpers.HertzToHertz;
            this.normalizeUnitSum = normalizeUnitSum;
            this.type = type;
            this.isLog = log;
            if (inputSize > 2)
            {
                this.CreateFilterBank(inputSize);
            }
        }

        /// <summary>
        /// Compute the triangle bands for the spectrum
        /// </summary>
        /// <param name="spectrum">
        /// The input spectrum.
        /// </param>
        /// <returns>
        /// The triangle banded spectrum.
        /// </returns>
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

        /// <summary>
        /// Create the filter bank of triangle bands for each frequency bin.
        /// </summary>
        /// <param name="inputSize">
        /// The size of the input spectrum.
        /// </param>
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
