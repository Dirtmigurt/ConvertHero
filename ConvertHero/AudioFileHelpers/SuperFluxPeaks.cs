namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// This algorithm detects peaks of an onset detection function computed by the SuperFluxNovelty algorithm. See SuperFluxExtractor for more details.
    /// </summary>
    public class SuperFluxPeaks
    {
        /// <summary>
        /// A helper object that can compute a moving average.
        /// </summary>
        MovingAverage averager;

        /// <summary>
        /// A helper object that can compute a max filter on a signal.
        /// </summary>
        MaxFilter maxFilter;

        /// <summary>
        /// look back duration for moving average filter [ms]
        /// </summary>
        int preAverage;

        /// <summary>
        /// look back duration for moving maximum filter [ms]
        /// </summary>
        int preMax;

        /// <summary>
        /// time threshold for double onsets detections (ms)
        /// </summary>
        float combine;

        /// <summary>
        /// threshold for peak peaking with respect to the difference between novelty_signal and average_signal (for onsets in ambient noise)
        /// </summary>
        float threshold;

        /// <summary>
        /// ratio threshold for peak picking with respect to novelty_signal/novelty_average rate, use 0 to disable it (for low-energy onsets)
        /// </summary>
        float ratioThreshold;

        /// <summary>
        /// 
        /// </summary>
        float startPeakTime;

        /// <summary>
        /// Holds a count of global peaks detected.
        /// </summary>
        int nDetect;

        /// <summary>
        /// hope size of the input novelty function
        /// </summary>
        int hopSize;

        /// <summary>
        /// frame rate of super flux novelty function.
        /// </summary>
        float frameRate;

        /// <summary>
        /// Initializes a new instance of the SuperFluxPeaks class, for picking peaks out of a SuperFluxNovely Onset detection function.
        /// </summary>
        /// <param name="frameRate">frame rate of super flux novelty function.</param>
        /// <param name="threshold">threshold for peak peaking with respect to the difference between novelty_signal and average_signal (for onsets in ambient noise)</param>
        /// <param name="ratioThreshold">ratio threshold for peak picking with respect to novelty_signal/novelty_average rate, use 0 to disable it (for low-energy onsets)</param>
        /// <param name="combine">time threshold for double onsets detections (ms)</param>
        /// <param name="preAvg">look back duration for moving average filter [ms]</param>
        /// <param name="preMax">look back duration for moving maximum filter [ms]</param>
        public SuperFluxPeaks(float frameRate = 172, float threshold = 0.05f, float ratioThreshold = 16, float combine = 30, int preAvg = 100, int preMax = 30)
        {
            this.frameRate = frameRate;
            this.threshold = threshold;
            this.ratioThreshold = ratioThreshold;

            // convert to frame indices
            this.preAverage = (int)(frameRate * preAvg / 1000f);
            this.preMax = (int)(frameRate * preMax / 1000f);

            if (this.preAverage <= 1)
            {
                throw new Exception($"SuperFluxPeaks: MovingAverage filter size must be > 1.");
            }

            if (this.preMax <= 1)
            {
                throw new Exception($"SuperFluxPeaks: MaxFilter filter size must be > 1.");
            }

            this.combine = combine / 1000f; // convert ms -> s
            this.averager = new MovingAverage(this.preAverage);
            this.maxFilter = new MaxFilter(this.preMax, true);
            this.startPeakTime = 0;
            this.nDetect = 0;
        }

        /// <summary>
        /// Find the peaks in the SuperFlux novelty signal.
        /// </summary>
        /// <param name="signal">The SuperFluxNovely curve.</param>
        /// <returns>The locations of the peaks in the input signal.</returns>
        public float[] Compute(float[] signal)
        {
            if (signal == null || signal.Length == 0)
            {
                return new float[0];
            }

            int size = signal.Length;
            float[] avg = this.averager.Compute(signal);
            float[] maxs = this.maxFilter.Filter(signal);
            List<float> peaks = new List<float>();
            for(int i = 0; i < size; i++)
            {
                // we want to avoid ratioThreshold noisy activation in really low flux parts so we set noise floor to 10-7 by default
                if (signal[i] == maxs[i] && signal[i] > 1e-8)
                {
                    bool isOverLinearThreshold = this.threshold > 0 && signal[i] > avg[i] + this.threshold;
                    bool isOverRatioThreshold = this.ratioThreshold > 0 && avg[i] > 0 && signal[i] / avg[i] > this.ratioThreshold;

                    if (isOverLinearThreshold || isOverRatioThreshold)
                    {
                        float peakTime = this.startPeakTime + (i / frameRate);
                        if ((peaks.Count > 0 && peakTime - peaks[peaks.Count - 1] > this.combine) || peaks.Count == 0)
                        {
                            peaks.Add(peakTime);
                            this.nDetect++;
                        }
                    }
                }
            }

            this.startPeakTime += size / this.frameRate;
            return peaks.ToArray();
        }
    }
}
