namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Compute the Contours of a Pitch Salience Function
    /// </summary>
    public class PitchContours
    {
        /// <summary>
        /// The sample rate of the audio signal
        /// </summary>
        float sampleRate;

        /// <summary>
        /// The hop size to use when reading a signal
        /// </summary>
        int hopSize;

        /// <summary>
        /// The bin resolution
        /// </summary>
        float binResolution;

        /// <summary>
        /// The peak frame threshold
        /// </summary>
        float peakFrameThreshold;

        /// <summary>
        /// The peak distribution threshold.
        /// </summary>
        float peakDistributionThreshold;

        /// <summary>
        /// The salient peaks bins
        /// </summary>
        List<List<float>> salientPeaksBins = new List<List<float>>();

        /// <summary>
        /// The values of the salient peaks
        /// </summary>
        List<List<float>> salientPeaksValues = new List<List<float>>();

        /// <summary>
        /// The non salient peaks bins
        /// </summary>
        List<List<float>> nonSalientPeaksBins = new List<List<float>>();

        /// <summary>
        /// The values of the non salient peaks.
        /// </summary>
        List<List<float>> nonSalientPeaksValues = new List<List<float>>();

        /// <summary>
        /// the time continuity in frames.
        /// </summary>
        float timeContinuityInFrames;

        /// <summary>
        /// the minimum duration of a salient pitch in frames
        /// </summary>
        float minDurationInFrames;

        /// <summary>
        /// The pitch continuity in bins.
        /// </summary>
        float pitchContinuityInBins;

        /// <summary>
        /// The numer of frames.
        /// </summary>
        int numberFrames;

        /// <summary>
        /// the duration of a frame.
        /// </summary>
        float frameDuration;

        /// <summary>
        /// Initializes a new instance of the PitchCountour class.
        /// </summary>
        /// <param name="sampleRate">the sampling rate of the audio signal [Hz]</param>
        /// <param name="hopSize">the hop size with which the pitch salience function was computed</param>
        /// <param name="binResolution">salience function bin resolution [cents]</param>
        /// <param name="peakFrameThreshold">per-frame salience threshold factor (fraction of the highest peak salience in a frame)</param>
        /// <param name="peakDistributionThreshold">allowed deviation below the peak salience mean over all frames (fraction of the standard deviation)</param>
        /// <param name="pitchContinuity">pitch continuity cue (maximum allowed pitch change durig 1 ms time period) [cents]</param>
        /// <param name="timeContinuity">time continuity cue (the maximum allowed gap duration for a pitch contour) [ms]</param>
        /// <param name="minDuration">the minimum allowed contour duration [ms]</param>
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

        /// <summary>
        /// Compute the pitch contours.
        /// </summary>
        /// <param name="peakBins">
        /// frame-wise array of cent bins corresponding to pitch salience function peaks
        /// </param>
        /// <param name="peakSaliences">
        /// frame-wise array of values of salience function peaks
        /// </param>
        /// <returns>
        /// contoursBins = array of frame-wise vectors of cent bin values representing each contour
        /// contoursSaliences = array of frame-wise vectors of pitch saliences representing each contour
        /// contoursStartTimes = array of start times of each contour [s]
        /// duration = time duration of the input signal [s]
        /// </returns>
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

        /// <summary>
        /// Track the maximally salient peak through all of the frames.
        /// </summary>
        /// <param name="index">
        /// Contains the starting frame of the maximally salient peak.
        /// </param>
        /// <param name="contourBins">
        /// contour bins.
        /// </param>
        /// <param name="contourSaliences">
        /// contour saliences.
        /// </param>
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

        /// <summary>
        /// Remove a peak at the specified index.
        /// </summary>
        /// <param name="peaksBins">
        /// The peaks bins
        /// </param>
        /// <param name="peaksValues">
        /// The peaks values
        /// </param>
        /// <param name="i">
        /// the frame index
        /// </param>
        /// <param name="j">
        /// the peak index.
        /// </param>
        private void RemovePeak(List<List<float>> peaksBins, List<List<float>> peaksValues, int i, int j)
        {
            peaksBins[i].RemoveAt(j);
            peaksValues[i].RemoveAt(j);
        }

        /// <summary>
        /// Find the next peak in the contour bins
        /// </summary>
        /// <param name="peaksBins">
        /// the peak bins.
        /// </param>
        /// <param name="contourBins">
        /// the contour bins.
        /// </param>
        /// <param name="i">
        /// Frame to search in for the next peak
        /// </param>
        /// <param name="backward">
        /// direction to search for the peak.
        /// </param>
        /// <returns></returns>
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
