using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class PeakDetection
    {
        float minPosition;
        float maxPosition;
        float threshold;
        int maxPeaks;
        float range;
        bool interpolate;
        OrderByType orderby;
        float minPeakDistance;

        public PeakDetection(float minPos = 0, float maxPos = -1, float threshold = 1e-6f, int maxPeaks= -1, float range = 1, bool interpolate = true, OrderByType orderby = OrderByType.Position, float minPeakDistance = 0)
        {
            this.minPosition = minPos;
            this.maxPosition = maxPos;
            this.threshold = threshold;
            this.maxPeaks = maxPeaks;
            this.range = range;
            this.interpolate = interpolate;
            this.orderby = orderby;
            this.minPeakDistance = minPeakDistance;
        }

        public (float[] positions, float[] amplitudes) Compute(float[] signal)
        {
            List<Peak> peaks = new List<Peak>(signal.Length);
            int size = signal.Length;
            float scale = this.range / (signal.Length - 1);

            // we want to round up to the next integer instead of simple truncation,
            // otherwise the peak frequency at i can be lower than _minPos
            int i = Math.Max(0, (int)Math.Ceiling(this.minPosition / scale));

            // first check the boundaries:
            if (i + 1 < size && signal[i] > signal[i + 1])
            {
                if (signal[i] > this.threshold)
                {
                    peaks.Add(new Peak(i * scale, signal[i]));
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
                            resultBin = (i + j) * 0.5f;
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
                        peaks.Add(new Peak(resultBin * scale, resultVal));
                    }
                    break;
                }
            }

            // check upper boundary here, so peaks are already sorted by position
            float pos = this.maxPosition / scale;
            if (size - 2 < pos && pos <= size - 1 && signal[size - 1] > signal[size - 2])
            {
                if (signal[size - 1] > this.threshold)
                {
                    peaks.Add(new Peak((size - 1) * scale, signal[size - 1]));
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

        private static void Interpolate(float left, float middle, float right, int curBin, out float resultValue, out float resultBin)
        {
            float delta_x = 0.5f * ((left - right) / (left - (2 * middle) + right));
            resultBin = curBin + delta_x;
            resultValue = middle - 0.25f * (left - right) * delta_x;
        }
    }

    public class Peak
    {
        public float position;
        public float magnitude;

        public Peak(float pos, float mag)
        {
            this.position = pos;
            this.magnitude = mag;
        }

        public static bool operator< (Peak a, Peak b)
        {
            return a.magnitude < b.magnitude;
        }

        public static bool operator> (Peak a, Peak b)
        {
            return a.magnitude > b.magnitude;
        }

        public static bool operator<= (Peak a, Peak b)
        {
            return a.magnitude <= b.magnitude;
        }

        public static bool operator>= (Peak a, Peak b)
        {
            return a.magnitude >= b.magnitude;
        }

        public static bool operator== (Peak a, Peak b)
        {
            return (a.magnitude == b.magnitude) && (a.position == b.position);
        }

        public static bool operator!= (Peak a, Peak b)
        {
            return (a.magnitude != b.magnitude) || (a.position != b.position);
        }

        public override bool Equals(object obj)
        {
            var peak = obj as Peak;
            return peak != null &&
                   this.position == peak.position &&
                   this.magnitude == peak.magnitude;
        }

        public override int GetHashCode()
        {
            var hashCode = -1892222449;
            hashCode = hashCode * -1521134295 + position.GetHashCode();
            hashCode = hashCode * -1521134295 + magnitude.GetHashCode();
            return hashCode;
        }
    }
}
