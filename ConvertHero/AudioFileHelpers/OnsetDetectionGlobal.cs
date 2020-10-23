using MathNet.Numerics;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class OnsetDetectionGlobal
    {
        private const int smoothingWindowHalfSize = 8;
        private float sampleRate;
        private OnsetMethod method;
        private int frameSize;
        private int hopSize;

        private int minFrequencyBin;
        private int maxFrequencyBin;
        private int numberFFTBins;
        private int numberERBBands;
        private int histogramSize = 5;
        private int bufferSize = 11;
        private int maxPeriodODF;
        private List<float> weights = new List<float>();
        private List<float> rweights = new List<float>();

        private float[] phase1;
        private float[] phase2;
        private float[] spectrum1;

        private FrameCutter frameCutter;
        private Windowing windower;
        private ERBBands erbbands;
        private MovingAverage movingAverage;
        private AutoCorrelation autoCorrelation;

        public OnsetDetectionGlobal(float sampleRate, OnsetMethod method = OnsetMethod.Infogain, int frameSize = 2048, int hopSize = 512)
        {
            this.sampleRate = sampleRate;
            this.method = method;
            this.frameSize = frameSize;
            this.hopSize = hopSize;

            this.windower = new Windowing(WindowingType.Hann, 0);
            if (method == OnsetMethod.Infogain)
            {
                // reversed triangle weighting
                for(int i = 0; i < this.histogramSize; i++)
                {
                    float weight = 1 - i * 0.9f / this.histogramSize;
                    this.weights.Add(weight);
                    this.rweights.Insert(0, weight);
                }

                float minFrequency = 40;
                float maxFrequency = 5000;
                this.minFrequencyBin = (int)Math.Round(minFrequency * frameSize / sampleRate);
                this.maxFrequencyBin = (int)Math.Round(maxFrequency * frameSize / sampleRate) + 1;
                this.numberFFTBins = maxFrequencyBin - minFrequencyBin;
            }
            else if (method == OnsetMethod.BeatEmphasis)
            {
                this.numberERBBands = 40;
                this.numberFFTBins = (int)(frameSize / 2 + 1);
                this.phase1 = new float[this.numberFFTBins];
                this.phase2 = new float[this.numberFFTBins];
                this.spectrum1 = new float[this.numberFFTBins];
                this.erbbands = new ERBBands(this.numberFFTBins, this.numberERBBands, this.sampleRate, 80, this.sampleRate / 2, type: SpectrumType.Magnitude);
                this.movingAverage = new MovingAverage(2 * smoothingWindowHalfSize + 1);
                this.autoCorrelation = new AutoCorrelation(NormalizeType.Unbiased);

                this.maxPeriodODF = (int)Math.Round(5.944308390022676 * sampleRate / hopSize);
                float rayparam2 = (float)Math.Pow(Math.Round(43 * 512.0 / this.maxPeriodODF), 2);
                for(int i = 0; i < this.maxPeriodODF; i++)
                {
                    int tau = i + 1;
                    this.weights.Add((float)(tau / rayparam2 * Math.Exp(-0.5 * tau * tau / rayparam2)));
                }
            }
        }

        public float[] Compute(float[] signal)
        {
            if (signal == null || signal.Length == 0)
            {
                return new float[0];
            }

            this.frameCutter = new FrameCutter(signal, this.frameSize, this.hopSize, startFromZero: true);

            if (this.method == OnsetMethod.Infogain)
            {
                return this.ComputeInfogain();
            }
            else if(this.method == OnsetMethod.BeatEmphasis)
            {
                return this.ComputeBeatEmphasis();
            }

            return new float[signal.Length];
        }

        private float[] ComputeInfogain()
        {
            // Initialize buffer with zero filled lists.
            List<List<float>> buffer = new List<List<float>>();
            for(int i = 0; i < this.bufferSize; i++)
            {
                List<float> spec = new List<float>(this.numberFFTBins);
                for(int j = 0; j < this.numberFFTBins; j++)
                {
                    spec.Add(0);
                }

                buffer.Add(spec);
            }

            float[] histogramOld = new float[this.numberFFTBins];
            float[] histogramNew = new float[this.numberFFTBins];
            List<float> onsetDetections = new List<float>();
            while(true)
            {
                float[] frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.windower.Compute(ref frame);
                float[] spectrumMagnitude = Spectrum.ComputeMagnitudeSpectrum(frame);

                buffer.RemoveAt(0);

                List<float> bufferFrame = new List<float>(this.numberFFTBins);
                for(int i = this.minFrequencyBin; i <= this.maxFrequencyBin; i++)
                {
                    bufferFrame.Add(spectrumMagnitude[i]);
                }
                buffer.Add(bufferFrame);

                // compute weighted sum of magnitudes for each bin
                for(int b = 0; b < this.numberFFTBins; b++)
                {
                    // initialize bin
                    histogramOld[b] = 0;
                    histogramNew[b] = 0;
                    for(int i = 0; i < this.histogramSize; i++)
                    {
                        histogramOld[b] += buffer[i][b] * this.rweights[i];
                        histogramNew[b] += buffer[this.histogramSize + 1 + i][b] * this.weights[i];
                    }
                }

                float detection = 0;
                for(int b = 0; b < this.numberFFTBins; b++)
                {
                    if (histogramOld[b] == 0)
                    {
                        histogramOld[b] = 1;
                    }

                    if (histogramNew[b] == 0)
                    {
                        histogramNew[b] = float.Epsilon;
                    }

                    detection += (float)Math.Max(Math.Log(histogramNew[b] / histogramOld[b]) / Math.Log(2), 0);
                }

                onsetDetections.Add(detection);
            }

            return onsetDetections.ToArray();
        }

        private float[] ComputeBeatEmphasis()
        {
            float[] onsetDetections;
            float[] tempFFT = new float[this.numberFFTBins];
            float[] tempERB = null;
            List<List<float>> onsetERB = new List<List<float>>();
            for(int i = 0; i < this.numberERBBands; i++)
            {
                onsetERB.Add(new List<float>());
            }

            int numberFrames = 0;
            while(true)
            {
                float[] frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.windower.Compute(ref frame);
                Complex32[] fft = Spectrum.ComputeFFT(frame);
                (float[] magnitude, float[] phase) = CartesianToPolar.ConvertComplexToPolar(fft);
                for(int i = 0; i < this.numberFFTBins; i++)
                {
                    float targetPhase = 2 * this.phase1[i] + this.phase2[i];
                    targetPhase = (float)(MathHelpers.FMod((targetPhase + Math.PI), (-2 * Math.PI)) + Math.PI);
                    tempFFT[i] = (float)((this.spectrum1[i] - Complex32.FromPolarCoordinates(magnitude[i], phase[i] - targetPhase))).Norm();
                }

                tempERB = this.erbbands.Compute(tempFFT);
                for(int b = 0; b < this.numberERBBands; b++)
                {
                    onsetERB[b].Add(tempERB[b]);
                }

                this.phase2 = this.phase1;
                this.phase1 = phase;
                this.spectrum1 = magnitude;
                numberFrames += 1;
            }

            // Post-processing found in M.Davies' matlab code, but not mentioned in the
            // paper, and skipped in this implementation:
            // - interpolate detection functions by factor of 2 (by zero-stuffing)
            // - half-rectify
            // - apply a Butterworth low-pass filter with zero-phase (running in forward
            // and backward directions); Matlab: [b,a]=butter(2,0.4);
            // - half-rectify again

            if (numberFrames == 0)
            {
                return null;
            }

            for(int b = 0; b < this.numberERBBands; b++)
            {
                // TODO tmp = interp(newspec2(pp,:),2);
                // interpolate to the doubled sampling rate interp performs lowpass
                // interpolation by inserting zeros into the original sequence and then
                // applying a special lowpass filter.

                // TODO half-rectify is not in the paper, futhermore, all onsetERB values
                // are supposed to be non-negative, as they are weighted sums of norms.
                // Half-rectification would have been necessary in the case of
                // interpolation, which can produce negative values.
                //for (size_t i=0; i<onsetERB[b].size(); ++i) {
                //  if (onsetERB[b][i] < 0) {
                //    onsetERB[b][i] = 0.;
                //  }
                //}

                // TODO newspecout(pp,:) = max(0,(filtfilt(b,a,(tmp))));
                // --> apply lowpass Butterworth filter, half-rectify again

                // Normalize to have a unit variance
                float bandMean = MathHelpers.Mean(onsetERB[b]);
                float bandStdDev = MathHelpers.StdDev(onsetERB[b], bandMean);
                if (bandStdDev > 0)
                {
                    for(int i = 0; i < onsetERB[b].Count; i++)
                    {
                        onsetERB[b][i] /= bandStdDev;
                    }
                }
            }

            // TODO Matlab: sbdb = max(0,newspecout); // half-rectify again? but onsetERB is
            // already non-negative

            // Compute weights for ODFs for ERB bands
            List<float> tempACF;
            float[] weightsERB = new float[this.numberERBBands];
            for(int b = 0; b < this.numberERBBands; b++)
            {
                // Apply adaptive moving average threshold to emphasise the strongest and
                // discard the least significant peaks. Subtract the adaptive mean, and
                // half-wave rectify the output, setting any negative valued elements to zero.

                // Align filter output for symmetrical averaging, and we want the filter to
                // return values on the edges as the averager output computed at these
                // positions to avoid smoothing to zero.

                float back = onsetERB[b].Last();
                float front = onsetERB[b].First();
                for(int i = 0; i < smoothingWindowHalfSize; i++)
                {
                    onsetERB[b].Add(back);
                    //onsetERB[b].Insert(0, front);
                    onsetERB[b].Add(back);
                }

                List<float> smoothed = new List<float>(this.movingAverage.Compute(onsetERB[b].ToArray()));
                for(int i = 0; i < smoothingWindowHalfSize; i++)
                {
                    smoothed.RemoveAt(0);
                    //smoothed.RemoveAt(smoothed.Count - 1);
                    smoothed.RemoveAt(0);
                }

                for(int i = 0; i < numberFrames; i++)
                {
                    onsetERB[b][i] -= smoothed[i];
                    if (onsetERB[b][i] < 0)
                    {
                        onsetERB[b][i] = 0;
                    }
                }

                // Compute the band-wise unbiased autocorrelation
                tempACF = new List<float>(this.autoCorrelation.Compute(onsetERB[b].ToArray()));
                while(tempACF.Count > this.maxPeriodODF)
                {
                    // Remove from the back until we get to the end.
                    tempACF.RemoveAt(tempACF.Count - 1);
                }

                // Weighten by tempo preference curve
                float[] tempACFWeighted = new float[this.maxPeriodODF];
                
                // Apply comb-filtering to reflect periodicities on different metric levels
                // (integer multiples) and apply tempo preference curve.
                int numberCombs = 4;

                // To accout for poor resolution of ACF at short lags, each comb element has
                // width proportional to its relationship to the underlying periodicity, and
                // its height is normalized by its width.

                // 0-th element in autocorrelation vector corresponds to the period of 1.
                // Min value for the 'region' variable is -3 => compute starting from the
                // 3-rd index, which corresponds to the period of 4, until period of 120
                // ODF samples (as in matlab code) or 110 (as in the paper). Generalization:
                // not clear why max period is 120 or 110, should be (512 - 3) / 4 = 127
                int periodMin = 4 - 1;
                int periodMax = (this.maxPeriodODF - (numberCombs - 1)) / numberCombs - 1;

                for(int comb = 1; comb <= numberCombs; comb++)
                {
                    int width = 2 * comb - 1;
                    for(int region = 1 - comb; region <= comb - 1; region++)
                    {
                        for(int period = periodMin; period < periodMax; period++)
                        {
                            tempACFWeighted[period] += this.weights[period] * tempACF[period * comb + region] / width;
                        }
                    }
                }

                // We are not interested in the period estimation, but in general salience of
                // the existing periodicity
                weightsERB[b] = tempACFWeighted.Max();
            }

            MathHelpers.Normalize(ref weightsERB);

            // Matlab M.Davies: take top 40% of weights, zero the rest (not in the paper!)
            List<float> sorted = weightsERB.OrderBy(f => f).ToList();
            float threshold = sorted[Math.Min((int)(sorted.Count * 0.6), sorted.Count - 1)];

            // Compute weighted sub of ODFs for ERB bands for each audio frame
            onsetDetections = new float[numberFrames];
            for (int i = 0; i < numberFrames; ++i)
            {
                for (int b = 0; b < this.numberERBBands; b++)
                {
                    if (weightsERB[b] >= threshold)
                    {
                        onsetDetections[i] += onsetERB[b][i] * weightsERB[b];
                    }
                }
            }

            return onsetDetections;
        }
    }

    public enum OnsetMethod
    {
        Infogain,
        BeatEmphasis
    }
}
