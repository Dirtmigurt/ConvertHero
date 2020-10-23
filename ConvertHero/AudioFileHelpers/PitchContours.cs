using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class PitchContours
    {
        float sampleRate;
        int hopSize;
        float binResolution;
        float peakFrameThreshold;
        float peakDistributionThreshold;

        List<List<float>> salientPeaksBins = new List<List<float>>();
        List<List<float>> salientPeaksValues = new List<List<float>>();
        List<List<float>> nonSalientPeaksBins = new List<List<float>>();
        List<List<float>> nonSalientPeaksValues = new List<List<float>>();

        float timeContinuityInFrames;
        float minDurationInFrames;
        float pitchContinuityInBins;
        int numberFrames;
        float frameDuration;

        public PitchContours(float sampleRate = 44100, int hopSize = 128, float binResolution = 10, float peakFrameThreshold = 0.9f, float peakDistributionThreshold = 0.9f, float pitchContinuity = 27.5625f, float timeContinuity = 100, float minDuration = 100f)
        {
            this.sampleRate = sampleRate;
            this.hopSize = hopSize;
            this.binResolution = binResolution;
            this.peakFrameThreshold = peakFrameThreshold;
            this.peakDistributionThreshold = peakDistributionThreshold;

            this.timeContinuityInFrames = (timeContinuity / 1000f) * this.sampleRate / this.hopSize;
            this.minDurationInFrames = (minDuration / 1000f) * this.sampleRate / this.hopSize;
            this.pitchContinuityInBins = pitchContinuity * 1000f * this.hopSize / this.sampleRate / this.binResolution;
            this.frameDuration = this.hopSize / this.sampleRate;
        }

        public (List<float[]> contoursBins, List<float[]> contoursSaliences, float[] contoursStartTimes, float duration) Compute(List<float[]> peakBins, List<float[]> peakSaliences)
        {
            List<float[]> contoursBins = new List<float[]>();
            List<float[]> contoursSaliences = new List<float[]>();
            List<float> contoursStartTimes = new List<float>();

            // validate inputs
            if (peakBins.Count != peakSaliences.Count)
            {
                throw new Exception($"PitchContours: peakBins and peakSaliences must be the same size");
            }

            this.numberFrames = peakBins.Count();
            float duration = numberFrames * frameDuration;

            if (this.numberFrames == 0)
            {
                return (contoursBins, contoursSaliences, contoursStartTimes.ToArray(), duration);
            }

            for (int i = 0; i < this.numberFrames; i++)
            {

                if (peakBins[i].Length != peakSaliences[i].Length)
                {
                    throw new Exception("PitchContours: peakBins and peakSaliences input vectors must have the same size");
                }

                int numPeaks = peakBins[i].Length;
                if (numPeaks == 0)
                {
                    continue;
                }

                for (int j = 0; j < numPeaks; j++)
                {
                    if (peakSaliences[i][j] < 0)
                    {
                        throw new Exception("PitchContours: salience peaks values input must be non-negative");
                    }
                }
            }

            // per-frame filtering
            this.salientPeaksBins.Clear();
            this.salientPeaksValues.Clear();
            this.nonSalientPeaksBins.Clear();
            this.nonSalientPeaksValues.Clear();

            for(int i = 0; i < this.numberFrames; i++)
            {
                this.salientPeaksBins.Add(new List<float>());
                this.salientPeaksValues.Add(new List<float>());
                this.nonSalientPeaksBins.Add(new List<float>());
                this.nonSalientPeaksValues.Add(new List<float>());
            }


            List<(int, int)> salientInFrame = new List<(int, int)>();
            for (int i = 0; i < this.numberFrames; i++)
            {
                if (peakSaliences[i].Length == 0)
                { // avoiding that LINQ Max() will thow on empty array
                    continue;
                }

                float frameMinSalienceThreshold = this.peakFrameThreshold * peakSaliences[i].Max();
                for (int j = 0; j < peakBins[i].Length; j++)
                {
                    if (peakSaliences[i][j] < frameMinSalienceThreshold)
                    {
                        this.nonSalientPeaksBins[i].Add(peakBins[i][j]);
                        this.nonSalientPeaksValues[i].Add(peakSaliences[i][j]);
                    }
                    else
                    {
                        salientInFrame.Add((i, j));
                    }
                }
            }

            // gather distribution statistics for overall peak filtering
            List<float> allPeakValues = new List<float>();
            for(int i = 0; i < salientInFrame.Count; i++)
            {
                int ii = salientInFrame[i].Item1;
                int jj = salientInFrame[i].Item2;
                allPeakValues.Add(peakSaliences[ii][jj]);
            }

            float salienceMean = allPeakValues.Average();
            float overallMeanSalienceThreshold = salienceMean - MathHelpers.StdDev(allPeakValues, salienceMean) * this.peakDistributionThreshold;

            // distribution-based filtering
            for (int i = 0; i < salientInFrame.Count; i++)
            {
                int ii = salientInFrame[i].Item1;
                int jj = salientInFrame[i].Item2;
                if (peakSaliences[ii][jj] < overallMeanSalienceThreshold)
                {
                    this.nonSalientPeaksBins[ii].Add(peakBins[ii][jj]);
                    this.nonSalientPeaksValues[ii].Add(peakSaliences[ii][jj]);
                }
                else
                {
                    this.salientPeaksBins[ii].Add(peakBins[ii][jj]);
                    this.salientPeaksValues[ii].Add(peakSaliences[ii][jj]);
                }
            }

            // peak streaming
            long n = 0;
            while (true)
            {
                n++;
                int index;
                List<float> contourBins;
                List<float> contourSaliences;

                this.TrackPitchContour(out index, out contourBins, out contourSaliences);

                if (contourBins.Count > 0)
                {
                    // Check if contour exceeds the allowed minimum length. This requirement is not documented in
                    // the reference [1], but was reported in personal communication with the author.

                    if (contourBins.Count >= this.minDurationInFrames)
                    {
                        contoursStartTimes.Add(index * this.frameDuration);
                        contoursBins.Add(contourBins.ToArray());
                        contoursSaliences.Add(contourSaliences.ToArray());
                    }
                }
                else
                {
                    break;  // no new contour was found
                }
            }

            return (contoursBins, contoursSaliences, contoursStartTimes.ToArray(), duration);
        }

        private void TrackPitchContour(out int index, out List<float> contourBins, out List<float> contourSaliences)
        {
            index = 0;
            contourBins = new List<float>();
            contourSaliences = new List<float>();

            // find the highest salient peak through all frames
            int max_i = 0;
            int max_j = 0;
            float maxSalience = 0;

            for (int i = 0; i < this.numberFrames; i++)
            {
                if (this.salientPeaksValues[i].Count > 0)
                {
                    int j = MathHelpers.ArgMax(this.salientPeaksValues[i]);
                    if (this.salientPeaksValues[i][j] > maxSalience)
                    {
                        maxSalience = this.salientPeaksValues[i][j];
                        max_i = i;
                        max_j = j;
                    }
                }
            }

            if (maxSalience == 0)
            {
                // no salient peaks left in the set -> no new contours added
                return;
            }

            List<(int, int)> removeNonSalientPeaks = new List<(int, int)>();

            // start new contour with this peak
            index = max_i; // the starting index of the contour
            contourBins.Add(this.salientPeaksBins[index][max_j]);
            contourSaliences.Add(this.salientPeaksValues[index][max_j]);
            // remove the peak from salient peaks
            this.RemovePeak(this.salientPeaksBins, this.salientPeaksValues, index, max_j);

            // track forwards in time
            int gap = 0, best_peak_j;
            for (int i = index + 1; i < this.numberFrames; i++)
            {
                // find salient peaks in the next frame
                best_peak_j = this.FindNextPeak(this.salientPeaksBins, contourBins, i);
                if (best_peak_j >= 0)
                {
                    // salient peak was found
                    contourBins.Add(this.salientPeaksBins[i][best_peak_j]);
                    contourSaliences.Add(this.salientPeaksValues[i][best_peak_j]);
                    this.RemovePeak(this.salientPeaksBins, this.salientPeaksValues, i, best_peak_j);
                    gap = 0;
                }
                else
                {
                    // no peaks were found -> use non-salient ones
                    // track using non-salient peaks for up to 100 ms by default
                    if (gap + 1 > this.timeContinuityInFrames)
                    {
                        // this frame would already exceed the gap --> stop forward tracking
                        break;
                    }
                    best_peak_j = this.FindNextPeak(this.nonSalientPeaksBins, contourBins, i);
                    if (best_peak_j >= 0)
                    {
                        contourBins.Add(this.nonSalientPeaksBins[i][best_peak_j]);
                        contourSaliences.Add(this.nonSalientPeaksValues[i][best_peak_j]);
                        removeNonSalientPeaks.Add((i, best_peak_j));
                        gap += 1;
                    }
                    else
                    {
                        break; // no salient nor non-salient peaks were found -> end of contour
                    }
                }
            }

            // remove all included non-salient peaks from the tail of the contour,
            // as the contour should always finish with a salient peak
            for (int g = 0; g < gap; g++)
            { // FIXME is using erase() faster?
                contourBins.RemoveAt(contourBins.Count - 1);
                contourSaliences.RemoveAt(contourSaliences.Count - 1);
            }

            // track backwards in time
            if (index == 0)
            {
                // we reached the starting frame
                // check if the contour exceeds the allowed minimum length
                if (contourBins.Count < this.timeContinuityInFrames)
                {
                    contourBins.Clear();
                    contourSaliences.Clear();
                }

                return;
            }

            gap = 0;
            for (int i = index - 1; ;)
            {
                // find salient peaks in the previous frame
                best_peak_j = this.FindNextPeak(this.salientPeaksBins, contourBins, i, true);
                if (best_peak_j >= 0)
                {
                    // salient peak was found, insert forward contourBins.insert(contourBins.begin(), _salientPeaksBins[i][best_peak_j]);
                    contourBins.Insert(0, this.salientPeaksBins[i][best_peak_j]);
                    contourSaliences.Insert(0, this.salientPeaksValues[i][best_peak_j]);
                    this.RemovePeak(this.salientPeaksBins, this.salientPeaksValues, i, best_peak_j);
                    index--;
                    gap = 0;
                }
                else
                {
                    // no salient peaks were found -> use non-salient ones
                    if (gap + 1 > this.timeContinuityInFrames)
                    {
                        // this frame would already exceed the gap --> stop backward tracking
                        break;
                    }
                    best_peak_j = this.FindNextPeak(this.nonSalientPeaksBins, contourBins, i, true);
                    if (best_peak_j >= 0)
                    {
                        contourBins.Insert(0, this.nonSalientPeaksBins[i][best_peak_j]);
                        contourSaliences.Insert(0, this.nonSalientPeaksValues[i][best_peak_j]);
                        removeNonSalientPeaks.Add((i, best_peak_j));
                        index--;
                        gap += 1;
                    }
                    else
                    {
                        // no salient nor non-salient peaks were found -> end of contour
                        break;
                    }
                }

                // manual check of loop conditions, as size_t cannot be negative and, therefore, conditions inside "for" cannot be used
                if (i > 0)
                {
                    i--;
                }
                else
                {
                    break;
                }
            }

            // remove non-salient peaks for the beginning of the contour,
            // as the contour should start with a salient peak
            contourBins.RemoveRange(0, gap);
            contourSaliences.RemoveRange(0, gap);
            index += gap;

            // remove all employed non-salient peaks for the list of available peaks
            for (int r = 0; r < removeNonSalientPeaks.Count; r++)
            {
                int i_p = removeNonSalientPeaks[r].Item1;
                if (i_p < index || i_p > index + contourBins.Count)
                {
                    continue;
                }
                int j_p = removeNonSalientPeaks[r].Item2;
                this.RemovePeak(this.nonSalientPeaksBins, this.nonSalientPeaksValues, i_p, j_p);
            }
        }

        private void RemovePeak(List<List<float>> peaksBins, List<List<float>> peaksValues, int i, int j)
        {
            peaksBins[i].RemoveAt(j);
            peaksValues[i].RemoveAt(j);
        }

        private int FindNextPeak(List<List<float>> peaksBins, List<float> contourBins, int i, bool backward = false)
        {
            // order = 1 to search forewards, = -1 to search backwards
            // i refers to a frame to search in for the next peak
            float distance;
            int best_peak_j = -1;
            float previousBin;
            float bestPeakDistance = this.pitchContinuityInBins;

            for (int j = 0; j < peaksBins[i].Count; j++)
            {
                previousBin = backward ? contourBins[0] : contourBins[contourBins.Count - 1];
                distance = Math.Abs(previousBin - peaksBins[i][j]);

                if (distance < bestPeakDistance)
                {
                    best_peak_j = j;
                    bestPeakDistance = distance;
                }
            }
            return best_peak_j;
        }
    }
}
