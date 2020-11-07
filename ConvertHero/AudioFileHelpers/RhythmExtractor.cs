namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class RhythmExtractor
    {
        /// <summary>
        /// The sample rate of the input audio file.
        /// </summary>
        private float sampleRate;

        /// <summary>
        /// If the BPM varies by more than this, then it will be added to the 'estimates' array.
        /// </summary>
        private float periodTolerance = 5;

        /// <summary>
        /// The helper object that does most of the heavy lifting.
        /// </summary>
        private BeatTrackerMultifeature beatTracker;

        /// <summary>
        /// Creates a new RhythmExtractor
        /// </summary>
        /// <param name="sampleRate">The sampling rate of the audio signal. [Hz]</param>
        /// <param name="minTempo">the slowest tempo to detect [bpm]</param>
        /// <param name="maxTempo">the fastest tempo to detect [bpm]</param>
        public RhythmExtractor(float sampleRate = 44100, float minTempo = 40, float maxTempo = 240)
        {
            this.sampleRate = sampleRate;
            this.beatTracker = new BeatTrackerMultifeature(this.sampleRate, minTempo, maxTempo);
        }

        /// <summary>
        /// Compute the exact locations of the beats of the audio file.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="ReportProgress">A callback function to report progress to the caller.</param>
        /// <returns>
        /// bpm = the average tempo estimation of the entire signal [bpm]
        /// ticks = the estimated tick locations [s]
        /// confidence = confidence with which the ticks are detected
        /// estimates = the list of bpm estimates characterizing the bpm distribution for the signal [bpm]
        /// bpmIntervals = list of beats interval [s]
        /// </returns>
        public (float bpm, float[] ticks, float confidence, float[] estimates, float[] bpmIntervals) Compute(float[] signal, Action<string, double> ReportProgress = null)
        {
            (float confidence, float[] ticks) = this.beatTracker.ComputeCombinations(signal, ReportProgress);
            List<float> bpmIntervals = new List<float>();
            List<float> bpmEstimateList = new List<float>();
            for(int i = 1; i < ticks.Length; i++)
            {
                bpmIntervals.Add(ticks[i] - ticks[i - 1]);
                bpmEstimateList.Add(60f / bpmIntervals[bpmIntervals.Count - 1]);
            }

            ReportProgress?.Invoke("Estimating BPM", 98);

            // estimate bpm
            List<float> estimates = new List<float>();
            float bpm = 0;
            if (bpmEstimateList.Count > 0)
            {
                float closestBpm = 0;
                for(int i = 0; i < bpmEstimateList.Count; i++)
                {
                    bpmEstimateList[i] /= 2f;
                }

                List<float> countedBins = MathHelpers.BinCount(bpmEstimateList);
                closestBpm = 2 * MathHelpers.ArgMax(countedBins);
                for(int i = 0; i < bpmEstimateList.Count; i++)
                {
                    bpmEstimateList[i] *= 2;
                    if (Math.Abs(closestBpm - bpmEstimateList[i]) < this.periodTolerance)
                    {
                        estimates.Add(bpmEstimateList[i]);
                    }
                }

                if (estimates.Count < 1)
                {
                    bpm = closestBpm;
                }
                else
                {
                    bpm = estimates.Average();
                }
            }

            return (bpm, ticks, confidence, estimates.ToArray(), bpmIntervals.ToArray());
        }
    }
}
