using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class PitchContoursMelody
    {
        MovingAverage MovingAverage;
        FrameCutter FrameCutter;
        Windowing Windower;
        SpectralPeaks PeakDetector;

        float binResolution;
        float referenceFrequency;
        float voicingTolerance;
        bool voiceVibrato;
        float sampleRate;
        int hopSize;
        int filterIterations; // number of iterations in the octave errors/pitch outliers filtering process
        bool guessUnvoiced;

        float frameDuration;
        int numberFrames;
        int averagerShift;
        float outlierMaxDistance;
        float duplicateMaxDistance;
        float duplicateMinDistance;

        float vibratoPitchStdDev;

        float minBin;
        float maxBin;

        // Voice vibrato detection parameters
        int vibratoFrameSize;
        int vibratoHopSize;
        int vibratoZeroPaddingFactor;
        int vibratoFFTSize;
        float vibratoMinFrequency;
        float vibratoMaxFrequency;
        float vibratoDBDropLobe;
        float vibratoDBDropSecondPeak;

        float centToHertzBase;

        int[] contoursStartIndices;
        int[] contoursEndIndices;
        float[] contoursBinsMean;
        float[] contoursSaliencesTotal;  // total salience (sum of per-frame saliences)
        float[] contoursSaliencesMean;
        float[] contoursBinsStdDev;

        float[] melodyPitchMean;               // melody pitch mean function
        List<(int, int)> duplicates = new List<(int, int)>();   // pairs of contour duplicates
        List<int> contoursSelected = new List<int>();    // indices of the selected contours
        List<int> contoursIgnored  = new List<int>();     // indices of the ignored contours
        List<int> contoursSelectedInitially;
        List<int> contoursIgnoredInitially;
        int numberContours;

        /// <summary>
        ///  This algorithm converts a set of pitch contours into a sequence of predominant f0 values in Hz by taking the value of the most predominant contour in each frame."
        ///  
        ///  This algorithm is intended to receive its "contoursBins", "contoursSaliences", and "contoursStartTimes" inputs from the PitchContours algorithm. 
        ///  The "duration" input corresponds to the time duration of the input signal. The output is a vector of estimated pitch values and a vector of confidence values."
        ///  
        ///  Note that "pitchConfidence" can be negative in the case of "guessUnvoiced"=True: the absolute values represent the confidence, negative values correspond to segments for which non-salient contours where selected, zero values correspond to non-voiced segments."
        ///  When input vectors differ in size, or "numberFrames" is negative, an exception is thrown. Input vectors must not contain negative start indices nor negative bin and salience values otherwise an exception is thrown."
        ///  Recommended processing chain: (see [1]): EqualLoudness -> frame slicing with sample rate = 44100, frame size = 2048, hop size = 128 -> Windowing with Hann, x4 zero padding -> Spectrum -> SpectralPeaks -> PitchSalienceFunction -> PitchSalienceFunctionPeaks -> PitchContours."
        ///  References:"
        ///  [1] J. Salamon and E. Gómez, "Melody extraction from polyphonic music
        ///  signals using pitch contour characteristics," IEEE Transactions on Audio,
        ///  Speech, and Language Processing, vol. 20, no. 6, pp. 1759–1770, 2012.
        /// </summary>
        /// <param name="referenceFrequency">
        /// the reference frequency for Hertz to cent convertion [Hz], corresponding to the 0th cent bin
        /// </param>
        /// <param name="binResolution">
        /// salience function bin resolution [cents]
        /// </param>
        /// <param name="sampleRate">
        /// the sampling rate of the audio signal (Hz)
        /// </param>
        /// <param name="hopSize">
        /// the hop size with which the pitch salience function was computed
        /// </param>
        /// <param name="voicingTolerance">
        /// allowed deviation below the average contour mean salience of all contours (fraction of the standard deviation)
        /// </param>
        /// <param name="voiceVibrato">
        /// detect voice vibrato
        /// </param>
        /// <param name="filterIterations">
        /// number of interations for the octave errors / pitch outlier filtering process
        /// </param>
        /// <param name="guessUnvoiced">
        /// Estimate pitch for non-voiced segments by using non-salient contours when no salient ones are present in a frame
        /// </param>
        /// <param name="minFrequency">
        /// the minimum allowed frequency for salience function peaks (ignore contours with peaks below) [Hz]
        /// </param>
        /// <param name="maxFrequency">
        /// the minimum allowed frequency for salience function peaks (ignore contours with peaks above) [Hz]
        /// </param>
        public PitchContoursMelody(float referenceFrequency = 55, float binResolution = 10, float sampleRate = 44100, int hopSize = 128, float voicingTolerance = 0.2f, bool voiceVibrato = false, int filterIterations = 3, bool guessUnvoiced = false, float minFrequency = 80, float maxFrequency = 20000)
        {
            this.voicingTolerance = voicingTolerance;
            this.sampleRate = sampleRate;
            this.hopSize = hopSize;
            this.referenceFrequency = referenceFrequency;
            this.binResolution = binResolution;
            this.filterIterations = filterIterations;
            this.voiceVibrato = voiceVibrato;
            this.guessUnvoiced = guessUnvoiced;

            // minimum and maximum allowed cent bins for contours
            float binsInOctave = 1200f / this.binResolution;
            float numberBins = (float)Math.Floor(6000f / this.binResolution) - 1;
            this.minBin = (float)Math.Max(0, Math.Floor(binsInOctave * MathHelpers.Log2(minFrequency / this.referenceFrequency) + 0.5));
            this.maxBin = (float)Math.Min(numberBins, Math.Floor(binsInOctave * MathHelpers.Log2(maxFrequency / this.referenceFrequency) + 0.5));
            this.frameDuration = this.hopSize / this.sampleRate;

            float outlierWidth = 50;
            this.outlierMaxDistance = (1200 + outlierWidth) / this.binResolution;
            this.duplicateMaxDistance = this.outlierMaxDistance;
            this.duplicateMinDistance = (1200 - outlierWidth) / this.binResolution;

            int averagerSize = (int)Math.Floor(5 / this.frameDuration);
            averagerSize = averagerSize % 2 == 0 ? averagerSize + 1 : averagerSize; // force the size to odd
            this.averagerShift = averagerSize / 2;

            this.vibratoPitchStdDev = 40 / this.binResolution; // 40 cents

            // parameters for voice vibrato detection
            // frame size computed given that we need 350ms of audio
            float vibratoSampleRate = this.sampleRate / this.hopSize;
            this.vibratoFrameSize = (int)(0.35 * vibratoSampleRate);
            this.vibratoHopSize = 1;
            this.vibratoZeroPaddingFactor = 4;
            this.vibratoFFTSize = this.vibratoFrameSize * this.vibratoZeroPaddingFactor;
            this.vibratoFFTSize = (int)Math.Pow(2, Math.Ceiling(MathHelpers.Log2(this.vibratoFFTSize)));
            this.vibratoMinFrequency = 5;
            this.vibratoMaxFrequency = 8;
            this.vibratoDBDropLobe = 15;
            this.vibratoDBDropSecondPeak = 20;

            // conversion to hertz
            this.centToHertzBase = (float)Math.Pow(2, this.binResolution / 1200f);

            // configure algos
            this.MovingAverage = new MovingAverage(averagerSize);
            this.Windower = new Windowing(WindowingType.Hann, this.vibratoFFTSize - this.vibratoFrameSize);
            this.PeakDetector = new SpectralPeaks(vibratoSampleRate, 3, type: OrderByType.Amplitude);
        }

        public (float[] pitch, float[] pitchConfidence) Compute(List<float[]> contoursBins, List<float[]> contoursSaliences, float[] contoursStartTimes, float duration)
        {
            List<float> pitch = new List<float>();
            List<float> pitchConfidence = new List<float>();

            if (duration < 0)
            {
                throw new Exception($"PitchContoursMelody: specified duration of the input must be non-negative.");
            }

            this.numberFrames = (int)Math.Round(duration / this.frameDuration);
            this.numberContours = contoursBins.Count;

            if (this.numberContours != contoursSaliences.Count &&
               this.numberContours != contoursStartTimes.Length)
            {
                throw new Exception($"PitchContoursMelody: contoursBins, contoursSaliences, and contoursStartTimes input vectors must have the same size.");
            }

            for(int i = 0; i < this.numberFrames; i++)
            {
                pitch.Add(0);
                pitchConfidence.Add(0);
            }

            if (this.numberFrames == 0)
            {
                return (pitch.ToArray(), pitchConfidence.ToArray());
            }

            for (int i = 0; i < this.numberContours; i++)
            {
                if (contoursBins[i].Length != contoursSaliences[i].Length)
                {
                    throw new Exception("PitchContoursMelody: contoursBins and contoursSaliences input vectors must have the same size");
                }
                if (contoursStartTimes[i] < 0)
                {
                    throw new Exception("PitchContoursMelody: contoursStartTimes input vector must contain non-negative values");
                }
                for (int j = 0; j < contoursBins[i].Length; j++)
                {
                    if (contoursBins[i][j] < 0)
                    {
                        throw new Exception("PitchContoursMelody: contour bin numbers must be non-negative");
                    }
                    if (contoursSaliences[i][j] < 0)
                    {
                        throw new Exception("PitchContoursMelody: contour pitch saliences must be non-negative");
                    }
                }
            }

            // no contours -> zero pitch vector output
            if (contoursBins.Count == 0)
            {
                return (pitch.ToArray(), pitchConfidence.ToArray());
            }

            this.VoicingDetection(contoursBins, contoursSaliences, contoursStartTimes);
            this.DetectContourDuplicates(contoursBins);
            this.melodyPitchMean = new float[this.numberFrames];
            for(int i = 0; i < this.filterIterations; i++)
            {
                this.ComputeMelodyPitchMean(contoursBins);
                this.RemoveContourDuplicates();
                this.ComputeMelodyPitchMean(contoursBins);
                this.RemovePitchOutliers();
            }

            // final melody selection: for each frame, select the peak
            // belonging to the contour with the highest total salience
            float centBin = 0, hertz;
            for (int i = 0; i < this.numberFrames; i++)
            {
                float maxSalience = 0;
                float confidence = 0;
                for (int j = 0; j < this.contoursSelected.Count; j++)
                {
                    int jj = this.contoursSelected[j];
                    if (this.contoursStartIndices[jj] <= i && this.contoursEndIndices[jj] >= i)
                    {
                        // current frame belongs to this contour
                        int shift = i - this.contoursStartIndices[jj];
                        if (this.contoursSaliencesTotal[jj] > maxSalience)
                        {
                            maxSalience = this.contoursSaliencesTotal[jj];
                            confidence = this.contoursSaliencesMean[jj];
                            centBin = contoursBins[jj][shift];
                        }
                    }
                }

                if (maxSalience == 0 && this.guessUnvoiced)
                {
                    for (int j = 0; j < this.contoursIgnored.Count; j++)
                    {
                        int jj = this.contoursIgnored[j];
                        if (this.contoursStartIndices[jj] <= i && this.contoursEndIndices[jj] >= i)
                        {
                            // current frame belongs to this contour
                            int shift = i - this.contoursStartIndices[jj];
                            if (this.contoursSaliencesTotal[jj] > maxSalience)
                            {
                                maxSalience = this.contoursSaliencesTotal[jj]; // store salience with negative sign in the case of unvoiced frames
                                confidence = 0f - this.contoursSaliencesMean[jj];
                                centBin = contoursBins[jj][shift];
                            }
                        }
                    }
                }

                if (maxSalience != 0)
                {
                    // a peak was found, convert cent bins to Hertz
                    // slow formula: this.referenceFrequency * pow(2, centBin*_binResolution / 1200.0);
                    hertz = (float)(this.referenceFrequency * Math.Pow(this.centToHertzBase, centBin));
                }
                else
                {
                    hertz = 0;
                }

                pitch[i] = hertz;
                pitchConfidence[i] = confidence;
            }

            return (pitch.ToArray(), pitchConfidence.ToArray());
        }

        private void VoicingDetection(List<float[]> contoursBins, List<float[]> contoursSaliences, float[] contoursStartTimes)
        {
            this.contoursStartIndices = new int[this.numberContours];
            this.contoursEndIndices = new int[this.numberContours];
            this.contoursBinsMean = new float[this.numberContours];
            this.contoursSaliencesTotal = new float[this.numberContours];
            this.contoursSaliencesMean = new float[this.numberContours];
            this.contoursBinsStdDev = new float[this.numberContours];

            this.contoursSelected.Clear();
            this.contoursIgnored.Clear();

            float[] contoursBinsMin = new float[this.numberContours];
            float[] contoursBinsMax = new float[this.numberContours];

            for(int i = 0; i < this.numberContours; i++)
            {
                this.contoursBinsMean[i] = contoursBins[i].Average();
                this.contoursBinsStdDev[i] = MathHelpers.StdDev(contoursBins[i], this.contoursBinsMean[i]);
                this.contoursSaliencesMean[i] = contoursSaliences[i].Average();
                contoursBinsMin[i] = contoursBins[i].Min();
                contoursBinsMax[i] = contoursBins[i].Max();
            }

            float averageSalienceMean = this.contoursSaliencesMean.Average();
            float salienceThreshold = averageSalienceMean - this.voicingTolerance * MathHelpers.StdDev(this.contoursSaliencesMean, averageSalienceMean);

            // voicing detection
            for (int i = 0; i < this.numberContours; i++)
            {
                // ignore contours with peaks outside of the allowed range
                if (contoursBinsMin[i] >= this.minBin && contoursBinsMax[i] <= this.maxBin)
                {
                    if (this.contoursSaliencesMean[i] >= salienceThreshold || this.contoursBinsStdDev[i] > this.vibratoPitchStdDev
                        || this.DetectVoiceVibrato(contoursBins[i], this.contoursBinsMean[i]))
                    {
                        this.contoursStartIndices[i] = (int)Math.Round(contoursStartTimes[i] / this.frameDuration);
                        this.contoursEndIndices[i] = this.contoursStartIndices[i] + contoursBins[i].Length - 1;
                        this.contoursSaliencesTotal[i] = contoursSaliences[i].Sum();
                        this.contoursSelected.Add(i);
                    }
                    else
                    {
                        if (this.guessUnvoiced)
                        {
                            this.contoursStartIndices[i] = (int)Math.Round(contoursStartTimes[i] / this.frameDuration);
                            this.contoursEndIndices[i] = this.contoursStartIndices[i] + contoursBins[i].Length - 1;
                            this.contoursSaliencesTotal[i] = contoursSaliences[i].Sum();
                            this.contoursIgnored.Add(i);
                        }
                    }
                }
            }

            this.contoursSelectedInitially = this.contoursSelected;
            this.contoursIgnoredInitially = this.contoursIgnored;
        }

        /// <summary>
        ///   Algorithm details are taken from personal communication with Justin Salamon.There should be only one (and it should be
        ///   the highest) peak between 5 and 8 Hz, associated with human voice vibrato.If there is more than 1 peak in this interval,
        ///   we may not be sure in vibrato --> go to search in next frame.
        ///
        ///   Find the 2nd and the 3rd highest peaks above 8Hz (we don't care in peaks below 5Hz, and they are normally not expected
        ///   to appear). The second peak should be 15 dBs quieter, and the third peak should be 20 dBs quieter than the highest peak.
        ///   If so, the voice peak is prominent enough --> human voice vibrato found in the contour.
        /// </summary>
        /// <param name="contourBins"></param>
        /// <param name="binMean"></param>
        /// <returns></returns>
        private bool DetectVoiceVibrato(float[] contourBins, float binMean)
        {
            if (!this.voiceVibrato)
            {
                return false;
            }

            // subtract mean from the contour pitch trajectory
            for (int i = 0; i < contourBins.Length; i++)
            {
                contourBins[i] -= binMean;
            }

            // apply FFT and check for a prominent peak in the expected frequency range for human vibrato (5-8Hz)
            FrameCutter frameCutter = new FrameCutter(contourBins, this.vibratoFrameSize, this.vibratoHopSize, startFromZero: true);

            while (true)
            {
                // get a frame
                float[] frame = frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.Windower.Compute(ref frame);
                float[] spectrum = Spectrum.ComputeMagnitudeSpectrum(frame);
                (float[] peakFrequencies, float[] peakMagnitudes) = this.PeakDetector.Compute(spectrum);

                int numberPeaks = peakFrequencies.Length;
                if (numberPeaks == 0)
                {
                    continue;
                }

                if (peakFrequencies[0] < this.vibratoMinFrequency || peakFrequencies[0] > this.vibratoMaxFrequency)
                {
                    continue;
                }

                if (numberPeaks > 1)
                {  // there is at least one extra peak
                    if (peakFrequencies[1] <= this.vibratoMaxFrequency)
                    {
                        continue;
                    }
                    if (20 * Math.Log10(peakMagnitudes[0] / peakMagnitudes[1]) < this.vibratoDBDropLobe)
                    {
                        continue;
                    }
                }

                if (numberPeaks > 2)
                {  // there is a second extra peak
                    if (peakFrequencies[2] <= this.vibratoMaxFrequency)
                    {
                        continue;
                    }
                    if (20 * Math.Log10(peakMagnitudes[0] / peakMagnitudes[2]) < this.vibratoDBDropSecondPeak)
                    {
                        continue;
                    }
                }

                // prominent peak associated with voice is found
                return true;
            }

            return false;
        }

        /// <summary>
        /// To compare contour trajectories we compute the distance between their pitch values on a per-frame basis for the
        /// region in which they overlap, and compute the mean over this region.If the mean distance is within 1200+-50 cents,
        /// the contours are considered octave duplicates.
        ///
        /// There is no requirement on the length of overlap region, according to [1] and personal communication with the
        /// author, but it can be introduced.However, algorithm already works well without such a requirement.
        /// </summary>
        /// <param name="contoursBins"></param>
        private void DetectContourDuplicates(List<float[]> contoursBins)
        {
            this.duplicates.Clear();
            for (int i = 0; i < this.contoursSelected.Count; i++)
            {
                int ii =this.contoursSelected[i];

                for (int j = i + 1; j <this.contoursSelected.Count; j++)
                {
                    int jj =this.contoursSelected[j];
                    int start = 0, end = 0;
                    bool overlap = false;

                    if (this.contoursStartIndices[ii] >=this.contoursStartIndices[jj]
                        &&this.contoursStartIndices[ii] <=this.contoursEndIndices[jj])
                    {
                        // .......[CONTOUR1]......
                        // ....[CONTOUR2].........
                        // or
                        // .......[CONTOUR1]......
                        // ....[CONTOUR2.....]....
                        start =this.contoursStartIndices[ii];
                        end = Math.Min(this.contoursEndIndices[ii],this.contoursEndIndices[jj]);
                        overlap = true;
                    }
                    else if (this.contoursStartIndices[jj] <=this.contoursEndIndices[ii]
                        &&this.contoursStartIndices[jj] >=this.contoursStartIndices[ii])
                    {
                        // ....[CONTOUR1].........
                        // .......[CONTOUR2]......
                        // or
                        // ....[CONTOUR1.....]....
                        // .......[CONTOUR2]......
                        start = this.contoursStartIndices[jj];
                        end = Math.Min(this.contoursEndIndices[ii],this.contoursEndIndices[jj]);
                        overlap = true;
                    }
                    if (overlap)
                    {
                        // compute the mean distance for overlap region
                        float distance = 0;
                        int shift_i = start - this.contoursStartIndices[ii];
                        int shift_j = start - this.contoursStartIndices[jj];

                        for (int ioverlap = start; ioverlap <= end; ioverlap++)
                        {
                            distance += contoursBins[ii][shift_i] - contoursBins[jj][shift_j];
                            shift_i++;
                            shift_j++;
                        }
                        distance = Math.Abs(distance) / (end - start + 1);
                        // recode cents to bins
                        if (distance > this.duplicateMinDistance && distance < this.duplicateMaxDistance)
                        {
                            // contours ii and jj differ for around 1200 cents (i.e., 1 octave) --> they are duplicates
                           this.duplicates.Add((ii, jj));
                        }
                    }
                }
            }
        }

        /// <summary>
        ///   Additional suggestion by Justin Salamon: implement a soft bias against the lowest frequencies:
        ///   if f< 150Hz --> bias = f / 150Hz* 0.3
        ///   In our evaluation, the results are ok without such a bias, when only using a hard threshold of
        ///   80Hz for the minimum frequency allowed for salience peaks.Therefore the bias is not implemented.
        /// </summary>
        /// <param name="contoursBins"></param>
        private void ComputeMelodyPitchMean(List<float[]> contoursBins)
        {
            float sumSalience = 0;

            // compute melody pitch mean (weighted mean for all present contours) for each frame
            float previous = 0;
            for (int i = 0; i <this.numberFrames; i++)
            {
               this.melodyPitchMean[i] = 0;
                sumSalience = 0;
                for (int j = 0; j <this.contoursSelected.Count; j++)
                {
                    int jj =this.contoursSelected[j];
                    if (this.contoursStartIndices[jj] <= i &&this.contoursEndIndices[jj] >= i)
                    {
                        // current frame belongs to this contour
                        int shift = i -this.contoursStartIndices[jj];
                       this.melodyPitchMean[i] +=this.contoursSaliencesTotal[jj] * contoursBins[jj][shift];
                        sumSalience +=this.contoursSaliencesTotal[jj];
                    }
                }
                if (sumSalience > 0)
                {
                   this.melodyPitchMean[i] /= sumSalience;
                }
                else
                {
                    // no contour was found for current frame --> use value from previous bin
                   this.melodyPitchMean[i] = previous;
                }
                previous =this.melodyPitchMean[i];
            }

            // replace zeros from the beginnig by the first non-zero value
            for (int i = 0; i <this.numberFrames; i++)
            {
                if (this.melodyPitchMean[i] > 0)
                {
                    for (int j = 0; j < i; j++)
                    {
                       this.melodyPitchMean[j] =this.melodyPitchMean[i];
                    }
                    break;
                }
            }

            // run 5-second moving average filter to smooth melody pitch mean
            // we want to align filter output for symmetrical averaging,
            // and we want the filter to return values on the edges as the averager output computed at these positions
            // to avoid smoothing to zero
            List<float> temp = new List<float>(this.melodyPitchMean);
            float back = temp[temp.Count - 1];
            for(int i = 0; i < this.averagerShift; i++)
            {
                temp.Add(back);
                temp.Insert(0, temp[0]);
            }

            float[] smoothed = this.MovingAverage.Compute(temp.ToArray());
            for(int i = smoothed.Length - this.melodyPitchMean.Length; i < smoothed.Length; i++)
            {
                this.melodyPitchMean[i - smoothed.Length + this.melodyPitchMean.Length] = smoothed[i];
            }
        }

        private void RemoveContourDuplicates()
        {
            // each iteration we start with all contours that passed the voiding detection stage,
            // but use the most recently computed melody pitch mean.

            // reinitialize the list of selected contours
            this.contoursSelected = this.contoursSelectedInitially;
            this.contoursIgnored = this.contoursIgnoredInitially;

            // compute average melody pitch mean on the intervals corresponding to all contours
            float[] contoursMelodyPitchMean = new float[this.numberContours];
            for (int i = 0; i < this.contoursSelected.Count; i++)
            {
                int ii = this.contoursSelected[i];
                contoursMelodyPitchMean[ii] = MathHelpers.Accumulate(this.melodyPitchMean, this.contoursStartIndices[ii], this.contoursEndIndices[ii] + 1);
                contoursMelodyPitchMean[ii] /= (this.contoursEndIndices[ii] - this.contoursStartIndices[ii] + 1);
            }

            // for each duplicates pair, remove the contour furtherst from melody pitch mean
            for (int c = 0; c < this.duplicates.Count; c++)
            {
                int ii = this.duplicates[c].Item1;
                int jj = this.duplicates[c].Item2;
                float ii_distance = Math.Abs(this.contoursBinsMean[ii] - contoursMelodyPitchMean[ii]);
                float jj_distance = Math.Abs(this.contoursBinsMean[jj] - contoursMelodyPitchMean[jj]);
                if (ii_distance < jj_distance)
                {
                    // remove contour jj
                    this.contoursSelected.RemoveAll(m => m == jj);
                    if (this.guessUnvoiced)
                    {
                        this.contoursIgnored.Add(jj);
                    }
                }
                else
                {
                    // remove contour ii
                    this.contoursSelected.RemoveAll(m => m == ii);
                    if (this.guessUnvoiced)
                    {
                        this.contoursIgnored.Add(ii);
                    }
                }
            }
        }

        private void RemovePitchOutliers()
        {
            // compute average melody pitch mean on the intervals corresponding to all contour
            // remove pitch outliers by deleting contours at a distance more that one octave from melody pitch mean

            //foreach(int ii in this.contoursSelected)
            for(int i = 0; i < this.contoursSelected.Count;)
            {
                int ii = this.contoursSelected[i];
                float contourMelodyPitchMean = MathHelpers.Accumulate(this.melodyPitchMean, this.contoursStartIndices[ii], this.contoursEndIndices[ii]);
                contourMelodyPitchMean /= (this.contoursEndIndices[ii] - this.contoursStartIndices[ii] + 1);
                if (Math.Abs(this.contoursBinsMean[ii] - contourMelodyPitchMean) > this.outlierMaxDistance)
                {
                    // remove contour
                    this.contoursSelected.RemoveAt(i);
                    if (this.guessUnvoiced)
                    {
                        this.contoursIgnored.Add(ii);
                    }
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
