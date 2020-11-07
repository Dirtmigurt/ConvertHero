namespace ConvertHero.AudioFileHelpers
{
    using System.Collections.Generic;

    public class SuperFluxExtractor
    {
        /// <summary>
        /// These frequency bands have been optimized for the best general onset detection.
        /// They are taken from the reference implementation in python at https://github.com/CPJKU/SuperFlux
        /// </summary>
        private static readonly float[] SuperFluxBands = {21.533203125f, 43.06640625f, 64.599609375f, 86.1328125f, 107.666015625f, 129.19921875f, 150.732421875f, 172.265625f, 193.798828125f, 215.33203125f, 236.865234375f, 258.3984375f, 279.931640625f, 301.46484375f, 322.998046875f, 344.53125f, 366.064453125f, 387.59765625f, 409.130859375f, 430.6640625f, 452.197265625f, 473.73046875f, 495.263671875f, 516.796875f, 538.330078125f, 559.86328125f, 581.396484375f, 602.9296875f, 624.462890625f, 645.99609375f, 667.529296875f, 689.0625f, 710.595703125f, 732.12890625f, 753.662109375f, 775.1953125f, 796.728515625f, 839.794921875f, 861.328125f, 882.861328125f, 904.39453125f, 925.927734375f, 968.994140625f, 990.52734375f, 1012.060546875f, 1055.126953125f, 1076.66015625f, 1098.193359375f, 1141.259765625f, 1184.326171875f, 1205.859375f, 1248.92578125f, 1270.458984375f, 1313.525390625f, 1356.591796875f, 1399.658203125f, 1442.724609375f, 1485.791015625f, 1528.857421875f, 1571.923828125f, 1614.990234375f, 1658.056640625f, 1701.123046875f, 1765.72265625f, 1808.7890625f, 1873.388671875f, 1916.455078125f, 1981.0546875f, 2024.12109375f, 2088.720703125f, 2153.3203125f, 2217.919921875f, 2282.51953125f, 2347.119140625f, 2411.71875f, 2497.8515625f, 2562.451171875f, 2627.05078125f, 2713.18359375f, 2799.31640625f, 2885.44921875f, 2950.048828125f, 3036.181640625f, 3143.84765625f, 3229.98046875f, 3316.11328125f, 3423.779296875f, 3509.912109375f, 3617.578125f, 3725.244140625f, 3832.91015625f, 3940.576171875f, 4069.775390625f, 4177.44140625f, 4306.640625f, 4435.83984375f, 4565.0390625f, 4694.23828125f, 4844.970703125f, 4974.169921875f, 5124.90234375f, 5275.634765625f, 5426.3671875f, 5577.099609375f, 5749.365234375f, 5921.630859375f, 6093.896484375f, 6266.162109375f, 6459.9609375f, 6653.759765625f, 6847.55859375f, 7041.357421875f, 7256.689453125f, 7450.48828125f, 7687.353515625f, 7902.685546875f, 8139.55078125f, 8376.416015625f, 8613.28125f, 8871.6796875f, 9130.078125f, 9388.4765625f, 9668.408203125f, 9948.33984375f, 10249.8046875f, 10551.26953125f, 10852.734375f, 11175.732421875f, 11498.73046875f, 11843.26171875f, 12187.79296875f, 12553.857421875f, 12919.921875f, 13285.986328125f, 13673.583984375f, 14082.71484375f, 14491.845703125f, 14922.509765625f, 15353.173828125f, 15805.37109375f, 16257.568359375f};

        /// <summary>
        /// The number of bins to smooth with the max filter in the novelty function.
        /// </summary>
        private static readonly int BinWidth = 8;

        /// <summary>
        /// The number of frames that must be accumulated before computing the novelty
        /// </summary>
        private static readonly int FrameWidth = 2;

        /// <summary>
        /// Helper function to chop up input signal into frames for processing.
        /// </summary>
        FrameCutter frameCutter;

        /// <summary>
        /// Helper function to apply windowing functons to a frame.
        /// </summary>
        Windowing windower;

        /// <summary>
        /// Helper function to apply weighted banding to a spectrum.
        /// </summary>
        TriangularBands triangularBands;

        /// <summary>
        /// Helper function to compute the SuperFluxNovelty onset detection function for a spectrum.
        /// </summary>
        SuperFluxNovelty superFluxNovelty;

        /// <summary>
        /// Helper function to pick the peaks out of a SuperFluxNovelty ODF curve.
        /// </summary>
        SuperFluxPeaks superFluxPeaks;

        /// <summary>
        /// Initialize a new instance of the SuperFluxExtractor class.
        /// </summary>
        /// <param name="frameSize">the frame size for computing low-level features</param>
        /// <param name="hopSize">the hop size for computing low-level features</param>
        /// <param name="sampleRate">the audio sampling rate [Hz]</param>
        /// <param name="threshold">threshold for peak peaking with respect to the difference between novelty_signal and average_signal (for onsets in ambient noise)</param>
        /// <param name="ratioThreshold">ratio threshold for peak picking with respect to novelty_signal/novelty_average rate, use 0 to disable it (for low-energy onsets)</param>
        /// <param name="combine">time threshold for double onsets detections (ms)</param>
        public SuperFluxExtractor(int frameSize = 2048, int hopSize = 512, float sampleRate = 44100, float threshold = 0.05f, float ratioThreshold = 16, float combine = 20)
        {
            this.frameCutter = new FrameCutter(frameSize, hopSize);
            this.windower = new Windowing(WindowingType.Hann);
            this.triangularBands = new TriangularBands(SuperFluxBands);
            this.superFluxNovelty = new SuperFluxNovelty(BinWidth, FrameWidth);
            this.superFluxPeaks = new SuperFluxPeaks(sampleRate / hopSize, threshold, ratioThreshold, combine, 100, 30);
        }

        /// <summary>
        /// Compute the peaks of a SuperFlux novelty curve which is computed from the input signal.
        /// </summary>
        /// <param name="signal">
        /// The input audio signal.
        /// </param>
        /// <returns>
        /// The peaks of the SuperFlux novelty curve of the audio signal.
        /// </returns>
        public float[] ComputePeaks(float[] signal)
        {
            this.frameCutter.SetBuffer(signal);

            List<float[]> bandFrames = new List<float[]>();
            List<float> fluxNovelty = new List<float>();
            while(true)
            {
                float[] frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.windower.Compute(ref frame);
                float[] spectrum = Spectrum.ComputeMagnitudeSpectrum(frame);
                float[] bands = this.triangularBands.ComputeTriangleBands(spectrum);
                bandFrames.Add(bands);

                if(bandFrames.Count > FrameWidth + 1)
                {
                    fluxNovelty.Add(this.superFluxNovelty.Compute(bandFrames));
                    bandFrames.RemoveAt(0);
                }
                else
                {
                    fluxNovelty.Add(0);
                }
            }

            return this.superFluxPeaks.Compute(fluxNovelty.ToArray());
        }

        /// <summary>
        /// Compute only the SuperFlux novelty curve of the input signal.
        /// </summary>
        /// <param name="signal">
        /// The input audio signal.
        /// </param>
        /// <returns>
        /// The OnsetDetectionFuction defined by SuperFluxNovelty.
        /// </returns>
        public float[] ComputeNovelty(float[] signal)
        {
            this.frameCutter.SetBuffer(signal);

            List<float[]> bandFrames = new List<float[]>();
            List<float> fluxNovelty = new List<float>();
            while (true)
            {
                float[] frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.windower.Compute(ref frame);
                float[] spectrum = Spectrum.ComputeMagnitudeSpectrum(frame);
                float[] bands = this.triangularBands.ComputeTriangleBands(spectrum);
                bandFrames.Add(bands);

                if (bandFrames.Count > FrameWidth + 1)
                {
                    fluxNovelty.Add(this.superFluxNovelty.Compute(bandFrames));
                    bandFrames.RemoveAt(0);
                }
                else
                {
                    fluxNovelty.Add(0);
                }
            }

            return fluxNovelty.ToArray();
        }
    }
}
