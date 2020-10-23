using Melanchall.DryWetMidi.Smf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class TempoTapMaxAgreement
    {
        private const int numberBins = 40;
        private float minTickTime;
        private List<float> histogramBins;


        public TempoTapMaxAgreement()
        {
            this.minTickTime = 0f;

            // Assign histogram bin centers
            this.histogramBins = new List<float>(numberBins + 1);
            this.histogramBins.Add(-0.5f);
            float delta = 1f / (numberBins - 1);
            for(double bin = -0.5 + 1.5 * delta; bin < 0.5 - 1.5 * delta; bin += delta)
            {
                this.histogramBins.Add((float)bin);
            }

            this.histogramBins.Add(0.5f);

            for(int i = 0; i < this.histogramBins.Count - 1; i++)
            {
                this.histogramBins[i] = (this.histogramBins[i] + this.histogramBins[i + 1]) / 2f;
            }

            this.histogramBins.RemoveAt(this.histogramBins.Count - 1);
        }

        public (float confidence, float[] ticks) Compute(List<List<float>> tickCandidates)
        {
            List<List<float>> candidatesClone = new List<List<float>>();
            // check validity of tickCandidates
            for(int i = 0; i < tickCandidates.Count; i++)
            {
                for(int j = 0; j < tickCandidates[i].Count; j++)
                {
                    if (tickCandidates[i][j] < 0)
                    {
                        throw new Exception($"TempoTapMaxAgreement: Tick values must be non-negative since they unit is time (t).");
                    }

                    if (j >= 1 && tickCandidates[i][j] <= tickCandidates[i][j - 1])
                    {
                        throw new Exception($"TempoTapMaxAgreement: Tick values must be monotonically increasing.");
                    }
                }
                candidatesClone.Add(new List<float>(tickCandidates[i]));
            }

            // Return no ticks if no candidates are provided.
            if(tickCandidates.Count == 0)
            {
                return (0, null);
            }

            // Remove ticks that are within the first this.minTickTime
            foreach(List<float> ticks in candidatesClone)
            {
                this.RemoveFirstSeconds(ticks);
            }

            int numberMethods = candidatesClone.Count;
            float[,] infogain = new float[numberMethods, numberMethods];
            for(int i = 0; i < numberMethods; i++)
            {
                for(int j = 0; j < numberMethods; j++)
                {
                    infogain[i, j] = this.ComputeBeatInfogain(candidatesClone[i], candidatesClone[j]);
                }
            }

            List<float> temp1 = new List<float>(2 * numberMethods);
            List<float> distanceInfogain = new List<float>(numberMethods);

            for(int i = 0; i < numberMethods; i++)
            {
                for(int j = i + 1; j < numberMethods; j++)
                {
                    temp1.Add(infogain[i, j]);
                }

                for(int j = 0; j < i; j++)
                {
                    temp1.Add(infogain[j, i]);
                }

                distanceInfogain.Add(temp1.Average());
                temp1.Clear();
            }

            int selectedMethod = MathHelpers.ArgMax(distanceInfogain);
            return (distanceInfogain.Average(), tickCandidates[selectedMethod].ToArray());
        }

        public float ComputeBeatInfogain(List<float> ticks1, List<float> ticks2)
        {
            if (ticks1.Count < 2 || ticks2.Count < 2)
            {
                return 0;
            }

            // ticks2 compared to ticks1
            List<float> forwardError = this.FindBeatError(ticks2, ticks1);
            float forwardEntropy = this.FindEntropy(forwardError);

            // ticks1 compared to ticks2
            List<float> backwardError = this.FindBeatError(ticks1, ticks2);
            float backwardEntropy = this.FindEntropy(backwardError);

            // find higher entropy value (i.e. which is the worst)
            float maxEntropy = Math.Max(forwardEntropy, backwardEntropy);
            return (float)(Math.Log(numberBins) / Math.Log(2)) - maxEntropy;
        }

        public List<float> FindBeatError(List<float> ticks1, List<float> ticks2)
        {
            List<float> beatError = new List<float>(ticks2.Count);

            for(int i = 0; i < ticks2.Count; i++)
            {
                float interval;
                int j = this.ClosestTick(ticks1, ticks2[i]);
                float error = ticks2[i] - ticks1[j];

                
                if (j == 0)    // first tick is the nearest
                {
                    interval = 0.5f * (ticks1[j + 1] - ticks1[j]);
                }
                else if (j == ticks1.Count - 1)    // last tick is the nearest
                {
                    interval = 0.5f * (ticks1[j] - ticks1[j - 1]);
                }
                else if (error < 0) // test if the error is positive or negative and choose interval accordingly
                {
                    interval = 0.5f * (ticks1[j] - ticks1[j - 1]);
                }
                else
                {
                    // nearest tick is after ticks2[i] --> look at the next interval
                    interval = 0.5f * (ticks1[j + 1] - ticks1[j]);
                }

                beatError.Add(0.5f * error / interval);
            }

            return beatError;
        }

        public float FindEntropy(List<float> beatError)
        {
            // fix the beat errors which are out of range in a way similar to princarg, but for [-0.5, 0.5]
            for(int i = 0; i < beatError.Count; i++)
            {
                beatError[i] = MathHelpers.FMod(beatError[i] + 0.5f, 1f) - 0.5f;
            }

            List<float> binValues = this.Histogram(beatError);

            // add the last bin frequency to the first bin
            binValues[0] += binValues[binValues.Count - 1];
            binValues.RemoveAt(binValues.Count - 1);

            MathHelpers.NormalizeSum(ref binValues);

            // compute the entropy
            float entropy = 0;
            for(int i = 0; i < binValues.Count; i++)
            {
                if (binValues[i] <= float.Epsilon)
                {
                    binValues[i] = 1;
                }

                entropy -= (float)(Math.Log(binValues[i]) / Math.Log(2)) * binValues[i];
            }

            binValues.Add(0);
            return entropy;
        }

        public int ClosestTick(List<float> ticks, float x)
        {
            float minDistance = -1;
            int j = 0;

            while(j < ticks.Count)
            {
                float distance = Math.Abs(ticks[j] - x);
                if (minDistance < 0 || distance < minDistance)
                {
                    minDistance = distance;
                }
                else
                {
                    break;
                }

                j++;
            }

            return j - 1;
        }

        public List<float> Histogram(List<float> array)
        {
            List<float> counter = new List<float>();
            for (int i = 0; i < this.histogramBins.Count + 1; i++)
            {
                counter.Add(0);
            }

            for(int i = 0; i < array.Count; i++)
            {
                if (array[i] >= this.histogramBins[this.histogramBins.Count - 1])
                {
                    counter[counter.Count - 1] += 1;
                }
                else
                {
                    for(int b = 0; b < this.histogramBins.Count; b++)
                    {
                        if (array[i] < this.histogramBins[b])
                        {
                            counter[b] += 1;
                            break;
                        }
                    }
                }
            }

            return counter;
        }


        /// <summary>
        /// Remove all values from the input list that are < this.minTickTime.
        /// </summary>
        /// <param name="ticks">
        /// The list to remove values from. This list must be in ascending sort order.
        /// </param>
        public void RemoveFirstSeconds(List<float> ticks)
        {
            if (ticks == null)
            {
                return;
            }

            while(ticks.Count > 0)
            {
                if (ticks[0] > this.minTickTime)
                {
                    break;
                }

                ticks.RemoveAt(0);
            }
        }
    }
}
