namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Class that contains helper methods for different kinds of normalization
    /// </summary>
    public static class SignalNormalization
    {
        /// <summary>
        /// Normalize within a window along the signal. (hop size = 1)
        /// </summary>
        /// <param name="signal">
        /// An input signal
        /// </param>
        /// <param name="window">
        /// Window size.
        /// </param>
        /// <returns></returns>
        public static List<float> WindowNormalize(List<float> signal, int window)
        {
            List<float> output = new List<float>();
            for(int i = 0; i < signal.Count; i++)
            {
                int windowStart = Math.Max(0, i - (window / 2));
                int windowEnd = Math.Min(signal.Count, i + (window / 2));
                if (windowEnd - windowStart < window)
                {
                    // we hit a boundary and our window isnt the correct size, try to extend in the backward direction
                    int missing = window - (windowEnd - windowStart);
                    if (windowStart > 0)
                    {
                        windowStart = Math.Max(0, windowStart - missing);
                    }
                    
                    if (windowEnd < signal.Count)
                    {
                        windowEnd = Math.Min(signal.Count, windowEnd + missing);
                    }
                }

                // Get the local max
                float max = 0;
                for (int j = windowStart; j < windowEnd; j++)
                {
                    if (signal[j] > max)
                    {
                        max = signal[j];
                    }
                }

                output.Add(signal[i] / max);
            }

            return output;
        }

        /// <summary>
        /// Subtract the signal mean from each sample and then normalize it to [0, 1]
        /// </summary>
        /// <param name="signal"></param>
        public static void MeanCenterNormalize(List<float> signal)
        {
            float mean = signal.Average();
            float max = signal.Max() - mean;
            for(int i = 0; i < signal.Count; i++)
            {
                if(signal[i] < mean)
                {
                    signal[i] = 0;
                }
                else
                {
                    signal[i] = (signal[i] - mean) / max;
                }
            }
        }

        /// <summary>
        /// Subtract the signal mean from each sample and then normalize it to [0, 1]
        /// </summary>
        /// <param name="signal"></param>
        public static void MeanCenterNormalize(float[] signal)
        {
            float mean = signal.Average();
            float max = signal.Max() - mean;
            for (int i = 0; i < signal.Length; i++)
            {
                if (signal[i] < mean)
                {
                    signal[i] = 0;
                }
                else
                {
                    signal[i] = (signal[i] - mean) / max;
                }
            }
        }

        /// <summary>
        /// Normalize a signal by dividing each elemeny by the max.
        /// </summary>
        /// <param name="signal"></param>
        public static void Normalize(float[] signal)
        {
            float max = Math.Max(float.Epsilon, signal.Max());
            for (int i = 0; i < signal.Length; i++)
            {
                signal[i] /= max;
            }
        }

        /// <summary>
        /// Normalize a signal by dividing each elemeny by the max.
        /// </summary>
        /// <param name="signal"></param>
        public static void Normalize(List<float> signal)
        {
            float max = Math.Max(float.Epsilon, signal.Max());
            for (int i = 0; i < signal.Count; i++)
            {
                signal[i] /= max;
            }
        }

        /// <summary>
        /// Subtract the signal median from each sample and then normalize it to [0, 1].
        /// This version clips the top 5th percentile of samples to 1.
        /// </summary>
        /// <param name="signal"></param>
        public static void MedianCenterNormalize(List<float> signal)
        {
            List<float> ordered = signal.Where(f => f > float.Epsilon).OrderBy(f => f).ToList();
            float median = ordered[ordered.Count / 2];
            float percentile95 = ordered[(int)Math.Min(ordered.Count - 1, ordered.Count * 0.95)];
            float max = percentile95 - median;
            for (int i = 0; i < signal.Count; i++)
            {
                if (signal[i] < median)
                {
                    signal[i] = 0;
                }
                else if (signal[i] > percentile95)
                {
                    signal[i] = 1f;
                }
                else
                {
                    signal[i] = (signal[i] - median) / max;
                }
            }
        }

        /// <summary>
        /// Subtract the signal median from each sample and then normalize it to [0, 1].
        /// This version clips the top 5th percentile of samples to 1.
        /// </summary>
        /// <param name="signal"></param>
        public static void MedianCenterNormalize(float[] signal)
        {
            List<float> ordered = signal.OrderBy(f => f).ToList();
            float median = ordered[signal.Length / 2];
            float percentile95 = ordered[(int)Math.Min(signal.Length - 1, signal.Length * 0.95)];
            float max = percentile95 - median;
            for (int i = 0; i < signal.Length; i++)
            {
                if (signal[i] < median)
                {
                    signal[i] = 0;
                }
                else if (signal[i] > percentile95)
                {
                    signal[i] = 1f;
                }
                else
                {
                    signal[i] = (signal[i] - median) / max;
                }
            }
        }

        /// <summary>
        /// Only normalize peaks that meet a certain threshold.
        /// </summary>
        /// <param name="signal"></param>
        public static void PeakNormalize(List<float> peaks, float percentOfPeaksToDrop = 10)
        {
            List<float> ordered = peaks.Where(f => f > float.Epsilon).OrderBy(f => f).ToList();
            float median = ordered[ordered.Count / 2];
            float percentile = ordered[(int)Math.Min(ordered.Count - 1, ordered.Count * (percentOfPeaksToDrop / 100f))];
            float max = ordered[ordered.Count - 1];
            for(int i = 0; i < peaks.Count; i++)
            {
                if (peaks[i] >= percentile)
                {
                    peaks[i] /= max;
                }
                else
                {
                    peaks[i] = 0;
                }
            }
        }

        /// <summary>
        /// Compute a histogram for a list of values.
        /// </summary>
        /// <param name="values">
        /// The list of values to count.
        /// </param>
        /// <param name="bins">
        /// The number of bins in the histogram.
        /// </param>
        /// <param name="percentBottomBinsToDrop">
        /// How many of the bottom bins should we ignore.
        /// </param>
        /// <returns>
        /// a histogram.
        /// </returns>
        public static float[] ComputeHistogram(List<float> values, int bins = 100, double percentBottomBinsToDrop = 5)
        {
            MeanCenterNormalize(values);
            int minAcceptableBin = (int)(bins * percentBottomBinsToDrop / 100.0);
            float[] hist = new float[bins];

            float max = values.Max();
            for(int i = 0; i < values.Count; i++)
            {
                int bin = (int)((values[i] / max) * (bins - 1));
                if(bin >= minAcceptableBin)
                {
                    hist[bin]++;
                }
                else
                {
                    values[i] = 0;
                }
            }

            return hist;
        }
    }
}
