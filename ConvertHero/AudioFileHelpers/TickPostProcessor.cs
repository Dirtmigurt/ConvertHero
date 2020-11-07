namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// This class provides helper methods for smoothing and cleaning up predicted beat markers in a song.
    /// </summary>
    public class TickPostProcessor
    {
        /// <summary>
        /// Clean up starting ticks that can appear very close together.
        /// If an integer multiple of the computed BPM falls into the desired BPM range, then interpolate/remove tick values
        /// to meet that BPM target.
        /// For example if a compute BPM of 90 is returned but the user wants 160 <= BPM <=200 then we can 2x that BPM to meet their needs.
        /// This is easily done by adding a single new tick in between each computed tick.
        /// </summary>
        /// <param name="ticks">
        /// The ticks that were computed from onset features.
        /// </param>
        /// <param name="bpm">
        /// The average BPM from the ticks in the 'ticks' parameter.
        /// </param>
        /// <param name="minTempo">
        /// The minimum tempo desired by the user.
        /// </param>
        /// <param name="maxTempo">
        /// The maximum tempo desired by the user.
        /// </param>
        /// <returns>
        /// A new tick array without the close together starting ticks.
        /// The new tick array may also have ticks added/or removed to change the BPM to an integer multiple.
        /// </returns>
        public static (float[] newTicks, float newBmp) PostProcessTicks(float[] ticks, float bpm, float minTempo, float maxTempo)
        {
            List<float> tickPeriods = new List<float>();
            for (int i = 1; i < ticks.Length; i++)
            {
                tickPeriods.Add(ticks[i] - ticks[i - 1]);
            }

            float average = tickPeriods.Average();
            float stdDev = MathHelpers.StdDev(tickPeriods, average);
            int index = 0;
            while (index < tickPeriods.Count && Math.Abs(tickPeriods[index] - average) > stdDev)
            {
                index++;
            }

            // skip ahead 4 more beats
            index = Math.Min(tickPeriods.Count - 1, index + 4);
            // work backward and overwrite the ticks
            for (; index > 0; index--)
            {
                float newTick = ticks[index] - average;
                ticks[index - 1] = newTick;
            }

            List<float> goodTicks = ticks.ToList();
            while (goodTicks.Count > 0 && goodTicks[0] < (average / 2))
            {
                goodTicks.RemoveAt(0);
            }

            // If the average bpm of the song fell within the range, then do not interpolate.
            if (bpm >= minTempo && bpm < maxTempo)
            {
                goodTicks = BeatAwareSmoothing(goodTicks);
                return (goodTicks.ToArray(), bpm);
            }

            int multiplier = 1;
            while (bpm * (multiplier + 1) < maxTempo)
            {
                multiplier++;
            }

            if (multiplier > 1 && bpm * multiplier > minTempo)
            {
                // interpolate ticks with (multiplier-1) intermediate ticks
                ticks = goodTicks.ToArray();
                for (int i = 0; i < ticks.Length - 1; i++)
                {
                    float start = ticks[i];
                    float end = ticks[i + 1];
                    float step = (end - start) / multiplier;
                    for (int j = 1; j < multiplier; j++)
                    {
                        goodTicks.Add(start + (j * step));
                    }
                }

                goodTicks.Sort();
                goodTicks = BeatAwareSmoothing(goodTicks);
                return (goodTicks.ToArray(), bpm * multiplier);
            }

            int diviser = 1;
            while (bpm / (diviser + 1f) > minTempo)
            {
                diviser++;
            }

            if (diviser > 1 && bpm / diviser < maxTempo)
            {
                // keep 1 element, remove diviser-1 
                int i = 0;
                while (i < goodTicks.Count)
                {
                    // remove goodTicks[i+1] ... goodTicks[i+diviser-1]
                    for (int j = i + 1; j <= i + diviser - 1; j++)
                    {
                        goodTicks.RemoveAt(j);
                    }

                    i++;
                }

                goodTicks = BeatAwareSmoothing(goodTicks);
                return (goodTicks.ToArray(), bpm / diviser);
            }

            goodTicks = BeatAwareSmoothing(goodTicks);
            return (goodTicks.ToArray(), bpm);
        }

        /// <summary>
        /// Smoothe weird spikes/dips in tempo that can throw the beat off the quarter note and onto the eigth note.
        /// </summary>
        /// <param name="goodTicks">
        /// The ticks that make have tempo spikes/dips.
        /// Its is very important that these have been hit with a moving average and are smoothed.
        /// </param>
        /// <returns>
        /// Ticks with spikes/dips that move the beat by 1/2 ticks removed.
        /// </returns>
        private static List<float> BeatAwareSmoothing(List<float> goodTicks)
        {
            List<float> returnCopy = new List<float>(goodTicks);
            List<float> beatPeriods = new List<float>();
            for (int i = 0; i < goodTicks.Count; i++)
            {
                if (i == 0)
                {
                    beatPeriods.Add(goodTicks[i]);
                }
                else
                {
                    beatPeriods.Add(goodTicks[i] - goodTicks[i - 1]);
                }
            }

            // build histogram
            int[] histogram = new int[(int)(1 + 60f / beatPeriods.Min())];
            for(int i = 0; i < beatPeriods.Count; i++)
            {
                histogram[(int)(60f / beatPeriods[i])]++;
            }

            List<int> bpmPeaks = new List<int>();
            int m = MathHelpers.ArgMax(histogram);
            while(histogram[m] > beatPeriods.Count / 20)
            {
                //decimate the histogram
                bpmPeaks.Add(m);
                int x = (int)Math.Ceiling(m / 25f); // +/- x represents a +/- 5% window around the peak bpm
                for(int i = Math.Max(0, m-x); i < Math.Min(histogram.Length, m+x); i++)
                {
                    histogram[i] = 0;
                }
                
                m = MathHelpers.ArgMax(histogram);
            }

            // This method assumes a single tempo throughout. Do not apply this to multi-tempo songs.
            if (bpmPeaks.Count > 1)
            {
                return goodTicks;
            }
            else if (bpmPeaks.Count == 0)
            {
                bpmPeaks.Add(m);
            }

            List<float> medians = new List<float>();
            foreach(int bpm in bpmPeaks)
            {
                // compute the average of all the bmps that hit this
                List<float> bpms = new List<float>();
                float range = bpm * 0.025f;
                for(int i = 0; i < beatPeriods.Count; i++)
                {
                    if (Math.Abs((60f / beatPeriods[i]) - bpm) <= range)
                    {
                        bpms.Add(60f / beatPeriods[i]);
                    }
                }

                medians.Add(60f / bpms.Average());
            }

            // The code below smooths out ticks/beat periods that slightly vary from some target median.
            // If a song is always the same tempo then this is fine.
            // If a song has > 1 tempo. this may need to be run multiple times where median=each tempo. In this case they would be peaks in a bpm histogram
            foreach(float median in medians)
            {
                int start = 0;
                while (start < beatPeriods.Count - 2 && !WithinPercent(beatPeriods[start], median, 0.01))
                {
                    start++;
                }

                while (true)
                {
                    // move start up to the next period thats different from the median
                    while (start < beatPeriods.Count - 2 && WithinPercent(beatPeriods[start], median, 0.01))
                    {
                        start++;
                    }

                    // move end until we get back to a a median beat period.
                    int end = start + 1;
                    float drift = median - beatPeriods[start];
                    while (end < beatPeriods.Count - 1 && !WithinPercent(beatPeriods[end], median, 0.01))
                    {
                        drift += median - beatPeriods[end];
                        end++;
                    }
                    drift += median - beatPeriods[end];

                    // sum up the drift from start -> end
                    float beatsDrifted = drift / median;

                    // only correct 0.5/1.0/1.5 beat errors, larger errors may simply be tempo changes.
                    if (end - start < 30 && Math.Abs(beatsDrifted) > 0.4 && Math.Abs(beatsDrifted) < 1.1)
                    {
                        // yikes from start -> end we have drifted at least half a beat!
                        // add drift to all ticks[end] => ticks.last
                        for (int j = end; j < returnCopy.Count; j++)
                        {
                            returnCopy[j] += drift;
                        }

                        for (int j = end - 1; j > start; j--)
                        {
                            returnCopy[j] = returnCopy[j + 1] - median;
                        }
                    }

                    start = end + 1;
                    if (start >= beatPeriods.Count - 1)
                    {
                        break;
                    }
                }

                goodTicks = SnapDriftingTicks(goodTicks, returnCopy, median);
                returnCopy = new List<float>(goodTicks);
                // beatperiods is computed from goodTicks which has changed.....
            }

            return returnCopy;
        }

        /// <summary>
        /// Ticks can drift slightly when applying the eighth note catch ups in the BeatAwareSmoothing method.
        /// This method looks at the ticks in the original signal and snaps them back their original positions.
        /// If the original signal was a 1/2 beat off then the original tick it should snap to will be exactly half-way in between
        /// two of the original ticks.
        /// </summary>
        /// <param name="originalTicks">
        /// The ogirinal ticks before any correction.
        /// </param>
        /// <param name="newTicks">
        /// The ticks after tempo dip/spike correction.
        /// </param>
        /// <param name="median">
        /// The most prominent tempo in the song.
        /// </param>
        /// <returns>
        /// The newTicks array but all ticks that could be snapped into place have been.
        /// </returns>
        private static List<float> SnapDriftingTicks(List<float> originalTicks, List<float> newTicks, float median)
        {
            List<float> returnTicks = new List<float>(newTicks);
            if (originalTicks.Count != newTicks.Count)
            {
                throw new ArgumentException($"OriginalTicks must be the same size as newTicks");
            }

            // compute the diffs between each element in goodTicks - returnTicks
            // When the diff hits a steady state, snap return ticks to the original ticks (may need to be interpolated for 0.5/1.5 beat differences)
            // steady state = standard deviation of diff is small for a set of 5 buckets
            List<float> tickDiffs = new List<float>(newTicks.Count);
            List<int> halfBeatDiffs = new List<int>(newTicks.Count);
            for (int i = 0; i < newTicks.Count; i++)
            {
                tickDiffs.Add(originalTicks[i] - newTicks[i]);
                halfBeatDiffs.Add((int)Math.Round(tickDiffs[i] / (median / 2)));
            }

            // compute moving stddev
            List<float> movingStdDev = new List<float>();
            List<float> buffer = new List<float>();
            int size = 5;
            for (int i = 0; i < size; i++)
            {
                buffer.Add(0);
            }

            for (int i = 0; i < newTicks.Count; i++)
            {
                // shift buffer over one
                buffer.Add(tickDiffs[i]);
                buffer.RemoveAt(0);

                movingStdDev.Add(MathHelpers.StdDev(buffer, buffer.Average()));
            }

            int start = 0;
            float threshold = 1e-3f;
            while (start < movingStdDev.Count - 2 && movingStdDev[start] < threshold)
            {
                start++;
            }

            while (true)
            {
                // move past the section with high stddev
                while(start < movingStdDev.Count - 2 && movingStdDev[start] > threshold)
                {
                    start++;
                }

                // find the end of this low deviation section
                int end = start + 1;
                while(end < movingStdDev.Count - 1 && movingStdDev[end] < threshold)
                {
                    end++;
                }

                // Correct some ticky bois
                if (halfBeatDiffs[start] != 0)
                {
                    // positive means that originalTick[start] > newTick[start], we corrected a tempo dip
                    if (Math.Abs(halfBeatDiffs[start]) % 2 == 1)
                    {
                        // /2 because these are halfBeat differences, it takes two half beats to make a whole beat diff.
                        int shift = halfBeatDiffs[start] / 2;
                        for (int j = start; j < end; j++)
                        {
                            int bound = j - shift;
                            if (bound <= 0 || bound >= originalTicks.Count)
                            {
                                continue;
                            }

                            float newTick = (originalTicks[bound] + originalTicks[bound - 1]) / 2f;
                            returnTicks[j] = newTick;
                        }
                    }
                    else
                    {
                        int shift = halfBeatDiffs[start] / 2;
                        for(int j = start; j < end; j++)
                        {
                            int bound = j - shift;
                            if (bound <= 0 || bound >= originalTicks.Count)
                            {
                                continue;
                            }

                            float newTick = originalTicks[bound];
                            returnTicks[j] = newTick;
                        }
                    }
                }

                start = end + 1;
                if (start >= returnTicks.Count - 1)
                {
                    break;
                }
            }

            return returnTicks;
        }

        /// <summary>
        /// Is a inside the range of +/- x% of b?
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="percent"></param>
        /// <returns></returns>
        private static bool WithinPercent(double a, double b, double x)
        {
            if (x > 1 && x <= 100)
            {
                x /= 100;
            }

            return Math.Abs(a - b) < x * b;
        }
    }
}
