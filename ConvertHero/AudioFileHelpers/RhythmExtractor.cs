using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class RhythmExtractor
    {
        private float sampleRate;
        private float periodTolerance = 5;
        private BeatTrackerMultifeature beatTracker;
        public RhythmExtractor(float sampleRate = 44100, float minTempo = 40, float maxTempo = 240)
        {
            this.sampleRate = sampleRate;
            this.beatTracker = new BeatTrackerMultifeature(this.sampleRate, minTempo, maxTempo);
        }

        public (float bpm, float[] ticks, float confidence, float[] estimates, float[] bpmIntervals)  Compute(float[] signal)
        {
            (float confidence, float[] ticks) = this.beatTracker.Compute(signal);
            List<float> bpmIntervals = new List<float>();
            List<float> bpmEstimateList = new List<float>();
            for(int i = 1; i < ticks.Length; i++)
            {
                bpmIntervals.Add(ticks[i] - ticks[i - 1]);
                bpmEstimateList.Add(60f / bpmIntervals[bpmIntervals.Count - 1]);
            }

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
