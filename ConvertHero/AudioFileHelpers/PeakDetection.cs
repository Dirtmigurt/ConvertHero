namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Detects peaks in an audio signal.
    /// </summary>
    public class PeakDetection
    {
        /// <summary>
        /// The min position that a peak can occur at
        /// </summary>
        float minPosition;

        /// <summary>
        /// The max position that a peak can occur at.
        /// </summary>
        float maxPosition;

        /// <summary>
        /// The minimum height of a peak.
        /// </summary>
        float threshold;

        /// <summary>
        /// The maximum number of peaks that should be returned.
        /// </summary>
        int maxPeaks;

        /// <summary>
        /// The range.
        /// </summary>
        float range;

        /// <summary>
        /// Interpolate between peaks
        /// </summary>
        bool interpolate;

        /// <summary>
        /// Order the output by Position/PeakMagnitude
        /// </summary>
        OrderByType orderby;

        /// <summary>
        /// Minimum distance that two peaks can be from each other.
        /// </summary>
        float minPeakDistance;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="minPos">
        /// The min position that a peak can occur at
        /// </param>
        /// <param name="maxPos">
        /// The max position that a peak can occur at.
        /// </param>
        /// <param name="threshold">
        /// The minimum height of a peak.
        /// </param>
        /// <param name="maxPeaks">
        /// The maximum number of peaks that should be returned.
        /// </param>
        /// <param name="range">
        /// The range.
        /// </param>
        /// <param name="interpolate">
        /// Interpolate between peaks
        /// </param>
        /// <param name="orderby">
        /// Order the output by Position/PeakMagnitude
        /// </param>
        /// <param name="minPeakDistance">
        /// Minimum distance that two peaks can be from each other.
        /// </param>
        public PeakDetection(float minPos = 0, float maxPos = -1, float threshold = 1e-6f, int maxPeaks= -1, float range = 1, bool interpolate = true, OrderByType orderby = OrderByType.Position, float minPeakDistance = 0)
        {
            this.minPosition = minPos;
            this.maxPosition = maxPos;
            this.threshold = threshold;
            this.maxPeaks = maxPeaks < 0 ? int.MaxValue : maxPeaks;
            this.range = range;
            this.interpolate = interpolate;
            this.orderby = orderby;
            this.minPeakDistance = minPeakDistance;
        }

        /// <summary>
        /// Determine which local maxima are peaks.
        /// </summary>
        /// <param name="spectrum">
        /// The input signal
        /// </param>
        /// <returns>
        /// The positions and amplitudes of the peaks in the signal.
        /// </returns>
        public (float[] positions, float[] amplitudes) ComputeOnSpectrum(float[] spectrum)
        {
            List<Peak> peaks = new List<Peak>(spectrum.Length);
            int size = spectrum.Length;
            float scale = this.range / (spectrum.Length - 1);

            // we want to round up to the next integer instead of simple truncation,
            // otherwise the peak frequency at i can be lower than _minPos
            int i = Math.Max(0, (int)Math.Ceiling(this.minPosition / scale));

            // first check the boundaries:
            if (i + 1 < size && spectrum[i] > spectrum[i + 1])
            {
                if (spectrum[i] > this.threshold)
                {
                    peaks.Add(new Peak(i * scale, spectrum[i]));
                }
            }

            while (true)
            {
                // going down
                while (i + 1 < size - 1 && spectrum[i] >= spectrum[i + 1])
                {
                    i++;
                }

                // now we're climbing
                while (i + 1 < size - 1 && spectrum[i] < spectrum[i + 1])
                {
                    i++;
                }

                // not anymore, go through the plateau
                int j = i;
                while (j + 1 < size - 1 && (spectrum[j] == spectrum[j + 1]))
                {
                    j++;
                }

                // end of plateau, do we go up or down?
                if (j + 1 < size - 1 && spectrum[j + 1] < spectrum[j] && spectrum[j] > this.threshold)
                { // going down again
                    float resultBin = 0.0f;
                    float resultVal = 0.0f;

                    if (j != i)
                    { // plateau peak between i and j
                        if (this.interpolate)
                        {
                            resultBin = (i + j) * 0.5f;
                        }
                        else
                        {
                            resultBin = i;
                        }
                        resultVal = spectrum[i];
                    }
                    else
                    { // interpolate peak at i-1, i and i+1
                        if (this.interpolate)
                        {
                            Interpolate(spectrum[j - 1], spectrum[j], spectrum[j + 1], j, out resultVal, out resultBin);
                        }
                        else
                        {
                            resultBin = j;
                            resultVal = spectrum[j];
                        }
                    }

                    float resultPos = resultBin * scale;

                    if (resultPos > this.maxPosition)
                    {
                        break;
                    }

                    peaks.Add(new Peak(resultPos, resultVal));
                }

                // nothing found, start loop again
                i = j;

                if (i + 1 >= size - 1)
                { // check the one just before the last position
                    if (i == size - 2 && spectrum[i - 1] < spectrum[i] &&
                        spectrum[i + 1] < spectrum[i] &&
                        spectrum[i] > this.threshold)
                    {
                        float resultBin = 0.0f;
                        float resultVal = 0.0f;
                        if (this.interpolate)
                        {
                            Interpolate(spectrum[i - 1], spectrum[i], spectrum[i + 1], j, out resultVal, out resultBin);
                        }
                        else
                        {
                            resultBin = i;
                            resultVal = spectrum[i];
                        }
                        peaks.Add(new Peak(resultBin * scale, resultVal));
                    }
                    break;
                }
            }

            // check upper boundary here, so peaks are already sorted by position
            float pos = this.maxPosition / scale;
            if (size - 2 < pos && pos <= size - 1 && spectrum[size - 1] > spectrum[size - 2])
            {
                if (spectrum[size - 1] > this.threshold)
                {
                    peaks.Add(new Peak((size - 1) * scale, spectrum[size - 1]));
                }
            }

            if (this.minPeakDistance > 0 && peaks.Count > 1)
            {
                List<int> deletedPeaks = new List<int>();
                float minPos;
                float maxPos;

                // order peaks by DESCENDING magnitude
                peaks = peaks.OrderByDescending(p => p.magnitude).ToList();

                int k = 0;
                while(k < peaks.Count - 1)
                {
                    minPos = peaks[k].position - this.minPeakDistance;
                    maxPos = peaks[k].position + this.minPeakDistance;

                    for( int l = k + 1; l < peaks.Count; l++)
                    {
                        if (peaks[l].position > minPos && peaks[l].position < maxPos)
                        {
                            deletedPeaks.Add(l);
                        }
                    }

                    // delete peaks starting from the end so the indexes are not altered
                    deletedPeaks = deletedPeaks.OrderByDescending(p => p).ToList();
                    for (int l = 0; l < deletedPeaks.Count; l++)
                    {
                        peaks.RemoveAt(deletedPeaks[l]);
                    }

                    deletedPeaks.Clear();
                    k++;
                }

                if (this.orderby == OrderByType.Position)
                {
                    peaks = peaks.OrderBy(p => p.position).ToList();
                }
            }
            else
            {
                if(this.orderby == OrderByType.Amplitude)
                {
                    peaks = peaks.OrderByDescending(p => p.magnitude).ToList();
                }
            }

            int nPeaks = Math.Min(this.maxPeaks, peaks.Count);
            float[] positions = new float[nPeaks];
            float[] amplitudes = new float[nPeaks];
            for(i = 0; i < nPeaks; i++)
            {
                positions[i] = peaks[i].position;
                amplitudes[i] = peaks[i].magnitude;
            }

            return (positions, amplitudes);
        }

        /// <summary>
        /// Determine which local maxima are peaks.
        /// </summary>
        /// <param name="signal">
        /// The input signal
        /// </param>
        /// <returns>
        /// The positions and amplitudes of the peaks in the signal.
        /// </returns>
        public (float[] positions, float[] amplitudes) ComputeOnSignal(float[] signal)
        {
            List<Peak> peaks = new List<Peak>(signal.Length);
            int size = signal.Length;

            // we want to round up to the next integer instead of simple truncation,
            // otherwise the peak frequency at i can be lower than _minPos
            int i = Math.Max(0, (int)Math.Ceiling(this.minPosition));

            // first check the boundaries:
            if (i + 1 < size && signal[i] > signal[i + 1])
            {
                if (signal[i] > this.threshold)
                {
                    peaks.Add(new Peak(i, signal[i]));
                }
            }

            while (true)
            {
                // going down
                while (i + 1 < size - 1 && signal[i] >= signal[i + 1])
                {
                    i++;
                }

                // now we're climbing
                while (i + 1 < size - 1 && signal[i] < signal[i + 1])
                {
                    i++;
                }

                // not anymore, go through the plateau
                int j = i;
                while (j + 1 < size - 1 && (signal[j] == signal[j + 1]))
                {
                    j++;
                }

                // end of plateau, do we go up or down?
                if (j + 1 < size - 1 && signal[j + 1] < signal[j] && signal[j] > this.threshold)
                { // going down again
                    float resultBin = 0.0f;
                    float resultVal = 0.0f;

                    if (j != i)
                    { // plateau peak between i and j
                        if (this.interpolate)
                        {
                            resultBin = (float)Math.Round((i + j) * 0.5f);
                        }
                        else
                        {
                            resultBin = i;
                        }
                        resultVal = signal[i];
                    }
                    else
                    { // interpolate peak at i-1, i and i+1
                        if (this.interpolate)
                        {
                            Interpolate(signal[j - 1], signal[j], signal[j + 1], j, out resultVal, out resultBin);
                        }
                        else
                        {
                            resultBin = j;
                            resultVal = signal[j];
                        }
                    }

                    peaks.Add(new Peak((float)Math.Round(resultBin), resultVal));
                }

                // nothing found, start loop again
                i = j;

                if (i + 1 >= size - 1)
                { // check the one just before the last position
                    if (i == size - 2 && signal[i - 1] < signal[i] &&
                        signal[i + 1] < signal[i] &&
                        signal[i] > this.threshold)
                    {
                        float resultBin = 0.0f;
                        float resultVal = 0.0f;
                        if (this.interpolate)
                        {
                            Interpolate(signal[i - 1], signal[i], signal[i + 1], j, out resultVal, out resultBin);
                        }
                        else
                        {
                            resultBin = i;
                            resultVal = signal[i];
                        }
                        peaks.Add(new Peak((float)Math.Round(resultBin), resultVal));
                    }
                    break;
                }
            }

            // check upper boundary here, so peaks are already sorted by position
            float pos = signal.Length - 1;
            if (size - 2 < pos && pos <= size - 1 && signal[size - 1] > signal[size - 2])
            {
                if (signal[size - 1] > this.threshold)
                {
                    peaks.Add(new Peak((size - 1), signal[size - 1]));
                }
            }

            if (this.minPeakDistance > 0 && peaks.Count > 1)
            {
                List<int> deletedPeaks = new List<int>();
                float minPos;
                float maxPos;

                // order peaks by DESCENDING magnitude
                peaks = peaks.OrderByDescending(p => p.magnitude).ToList();

                int k = 0;
                while (k < peaks.Count - 1)
                {
                    minPos = peaks[k].position - this.minPeakDistance;
                    maxPos = peaks[k].position + this.minPeakDistance;

                    for (int l = k + 1; l < peaks.Count; l++)
                    {
                        if (peaks[l].position > minPos && peaks[l].position < maxPos)
                        {
                            deletedPeaks.Add(l);
                        }
                    }

                    // delete peaks starting from the end so the indexes are not altered
                    deletedPeaks = deletedPeaks.OrderByDescending(p => p).ToList();
                    for (int l = 0; l < deletedPeaks.Count; l++)
                    {
                        peaks.RemoveAt(deletedPeaks[l]);
                    }

                    deletedPeaks.Clear();
                    k++;
                }

                if (this.orderby == OrderByType.Position)
                {
                    peaks = peaks.OrderBy(p => p.position).ToList();
                }
            }
            else
            {
                if (this.orderby == OrderByType.Amplitude)
                {
                    peaks = peaks.OrderByDescending(p => p.magnitude).ToList();
                }
            }

            int nPeaks = Math.Min(this.maxPeaks, peaks.Count);
            float[] positions = new float[nPeaks];
            float[] amplitudes = new float[nPeaks];
            for (i = 0; i < nPeaks; i++)
            {
                positions[i] = peaks[i].position;
                amplitudes[i] = peaks[i].magnitude;
            }

            return (positions, amplitudes);
        }

        /// <summary>
        /// Interpolate between samples
        /// </summary>
        /// <param name="left"></param>
        /// <param name="middle"></param>
        /// <param name="right"></param>
        /// <param name="curBin"></param>
        /// <param name="resultValue"></param>
        /// <param name="resultBin"></param>
        private static void Interpolate(float left, float middle, float right, int curBin, out float resultValue, out float resultBin)
        {
            float delta_x = 0.5f * ((left - right) / (left - (2 * middle) + right));
            resultBin = curBin + delta_x;
            resultValue = middle - 0.25f * (left - right) * delta_x;
        }
    }

    /// <summary>
    /// Class to hold all information about a peak in a single place.
    /// Also allows for the comparison of peaks
    /// </summary>
    public class Peak
    {
        /// <summary>
        /// The location of the peak (index)
        /// </summary>
        public float position;

        /// <summary>
        /// The magnitude of the peak
        /// </summary>
        public float magnitude;

        /// <summary>
        /// Initializes a new instaces of the Peak class.
        /// </summary>
        /// <param name="pos">The location of the peak (index)</param>
        /// <param name="mag">The magnitude of the peak</param>
        public Peak(float pos, float mag)
        {
            this.position = pos;
            this.magnitude = mag;
        }

        /// <summary>
        /// Determines how to peaks are compared for less than
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is less than Peak b
        /// </returns>
        public static bool operator< (Peak a, Peak b)
        {
            return a.magnitude < b.magnitude;
        }

        /// <summary>
        /// Determines how to peaks are compared for greater than
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is greater than Peak b
        /// </returns>
        public static bool operator> (Peak a, Peak b)
        {
            return a.magnitude > b.magnitude;
        }

        /// <summary>
        /// Determines how to peaks are compared for less than or equal to
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is less than or equal to Peak b
        /// </returns>
        public static bool operator<= (Peak a, Peak b)
        {
            return a.magnitude <= b.magnitude;
        }

        /// <summary>
        /// Determines how to peaks are compared for greater than or equal
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is greater than or equal Peak b
        /// </returns>
        public static bool operator>= (Peak a, Peak b)
        {
            return a.magnitude >= b.magnitude;
        }

        /// <summary>
        /// Determines how to peaks are compared for equality
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is equal to Peak b
        /// </returns>
        public static bool operator== (Peak a, Peak b)
        {
            return (a.magnitude == b.magnitude) && (a.position == b.position);
        }

        /// <summary>
        /// Determines how to peaks are compared for non-equality
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is not-equal to Peak b
        /// </returns>
        public static bool operator!= (Peak a, Peak b)
        {
            return (a.magnitude != b.magnitude) || (a.position != b.position);
        }

        /// <summary>
        /// Determines how to peaks are compared for equality
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>
        /// true if Peak a is equal to Peak b
        /// </returns>
        public override bool Equals(object obj)
        {
            var peak = obj as Peak;
            return peak != null &&
                   this.position == peak.position &&
                   this.magnitude == peak.magnitude;
        }

        /// <summary>
        /// Compute the hash code for a peak
        /// </summary>
        /// <returns>
        /// A unique int for each position/magnitude combination.
        /// </returns>
        public override int GetHashCode()
        {
            var hashCode = -1892222449;
            hashCode = hashCode * -1521134295 + position.GetHashCode();
            hashCode = hashCode * -1521134295 + magnitude.GetHashCode();
            return hashCode;
        }
    }
}
