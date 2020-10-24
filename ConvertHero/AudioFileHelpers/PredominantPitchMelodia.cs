namespace ConvertHero.AudioFileHelpers
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class PredominantPitchMelodia
    {
        /// <summary>
        /// Dictionary used to aggregate results of concurrent workers.
        /// </summary>
        ConcurrentDictionary<int, float[]> peakBinsDict = new ConcurrentDictionary<int, float[]>();

        /// <summary>
        /// Dictionary used to aggregate results of concurrent workers.
        /// </summary>
        ConcurrentDictionary<int, float[]> peakSalienceDict = new ConcurrentDictionary<int, float[]>();

        /// <summary>
        /// Frame cutter used to chop up the input audio signal into frames.
        /// </summary>
        FrameCutter frameCutter;

        /// <summary>
        /// Windower to apply window to frames.
        /// </summary>
        Windowing windower;
        
        /// <summary>
        /// Helper object that finds peaks in the spectrum
        /// </summary>
        SpectralPeaks spectralPeaks;

        /// <summary>
        /// Helper object that computes pitch salience
        /// </summary>
        PitchSalienceFunction pitchSalience;

        /// <summary>
        /// Helper object that find peaks in the pitch salience.
        /// </summary>
        PitchSalienceFunctionPeaks saliencePeaks;

        /// <summary>
        /// Helper object that finds pitch contours among pitch salience peaks.
        /// </summary>
        PitchContours pitchContours;

        /// <summary>
        /// Helper object that finds a melody within many pitch contours.
        /// </summary>
        PitchContoursMelody melodyDetector;

        #region
        /// <summary>
        /// List of windowers used by concurrent workers.
        /// </summary>
        List<Windowing> windowerList = new List<Windowing>();
        
        /// <summary>
        /// List of spectralPeak finders used by concurrent workers.
        /// </summary>
        List<SpectralPeaks> spectralPeaksList = new List<SpectralPeaks>();

        /// <summary>
        /// List of PitchSalienceFunction computers used by concurrent workers.
        /// </summary>
        List<PitchSalienceFunction> pitchSalienceList = new List<PitchSalienceFunction>();

        /// <summary>
        /// List of peak finders used by concurrent workers.
        /// </summary>
        List<PitchSalienceFunctionPeaks> saliencePeaksList = new List<PitchSalienceFunctionPeaks>();

        /// <summary>
        /// List of semaphores used to limit access to an index [i] to a single thread.
        /// </summary>
        List<SemaphoreSlim> semaphoreList = new List<SemaphoreSlim>();
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sampleRate">the sampling rate of the audio signal [Hz]</param>
        /// <param name="frameSize">the frame size for computing pitch salience</param>
        /// <param name="hopSize">the hop size with which the pitch salience function was computed</param>
        /// <param name="binResolution">salience function bin resolution [cents]</param>
        /// <param name="referenceFrequency">the reference frequency for Hertz to cent conversion [Hz], corresponding to the 0th cent bin</param>
        /// <param name="magnitudeThreshold">spectral peak magnitude threshold (maximum allowed difference from the highest peak in dBs)</param>
        /// <param name="magnitudeCompression">magnitude compression parameter for the salience function (=0 for maximum compression, =1 for no compression)</param>
        /// <param name="numberHarmonics">number of considered harmonics</param>
        /// <param name="harmonicWeight">harmonic weighting parameter (weight decay ratio between two consequent harmonics, =1 for no decay)</param>
        /// <param name="peakFrameThreshold">per-frame salience threshold factor (fraction of the highest peak salience in a frame)</param>
        /// <param name="peakDistributionThreshold">allowed deviation below the peak salience mean over all frames (fraction of the standard deviation)</param>
        /// <param name="pitchContinuity">pitch continuity cue (maximum allowed pitch change during 1 ms time period) [cents]</param>
        /// <param name="timeContinuity">time continuity cue (the maximum allowed gap duration for a pitch contour) [ms]</param>
        /// <param name="minDuration">the minimum allowed contour duration [ms]</param>
        /// <param name="voicingTolerance">allowed deviation below the average contour mean salience of all contours (fraction of the standard deviation)</param>
        /// <param name="voiceVibrato">detect voice vibrato</param>
        /// <param name="filterIterations">number of iterations for the octave errors / pitch outlier filtering process</param>
        /// <param name="guessUnvoiced">estimate pitch for non-voiced segments by using non-salient contours when no salient ones are present in a frame</param>
        /// <param name="minFrequency">the minimum allowed frequency for salience function peaks (ignore contours with peaks below) [Hz]</param>
        /// <param name="maxFrequency">the maximum allowed frequency for salience function peaks (ignore contours with peaks above) [Hz]</param>
        public PredominantPitchMelodia(
            /// Spectrum Processing
            float sampleRate = 44100, 
            int frameSize = 2048, 
            int hopSize = 128, 
            /// Pitch Salience Configuration
            float binResolution = 10, 
            float referenceFrequency = 55,
            float magnitudeThreshold = 40,
            float magnitudeCompression = 1,
            int numberHarmonics = 20,
            float harmonicWeight = 0.8f,
            /// Pitch contour tracking
            float peakFrameThreshold = 0.9f,
            float peakDistributionThreshold = 0.9f,
            float pitchContinuity = 27.5625f,
            float timeContinuity = 100,
            float minDuration = 100,
            /// Melody Detection
            float voicingTolerance = 0.2f,
            bool voiceVibrato = false,
            int filterIterations = 3,
            bool guessUnvoiced = false,
            float minFrequency = 80,
            float maxFrequency = 20000)
        {

            int zeroPaddingFactor = 4;
            int maxSpectralPeaks = 100;
            this.frameCutter = new FrameCutter(frameSize, hopSize, startFromZero: false);
            this.windower = new Windowing(WindowingType.Hann, (zeroPaddingFactor - 1) * frameSize);
            this.spectralPeaks = new SpectralPeaks(sampleRate, maxSpectralPeaks, 20000, 1, 0, OrderByType.Amplitude);
            this.pitchSalience = new PitchSalienceFunction(binResolution, referenceFrequency, magnitudeThreshold, magnitudeCompression, numberHarmonics, harmonicWeight);
            this.saliencePeaks = new PitchSalienceFunctionPeaks(binResolution, 1, 20000, referenceFrequency);
            this.pitchContours = new PitchContours(sampleRate, hopSize, binResolution, peakFrameThreshold, peakDistributionThreshold, pitchContinuity, timeContinuity, minDuration);
            this.melodyDetector = new PitchContoursMelody(referenceFrequency, binResolution, sampleRate, hopSize, voicingTolerance, voiceVibrato, filterIterations, guessUnvoiced, minFrequency, maxFrequency);

            // Initialize a bunch of these objects for multi threading!!
            for(int i = 0; i < 32; i++)
            {
                this.windowerList.Add(new Windowing(WindowingType.Hann, (zeroPaddingFactor - 1) * frameSize));
                this.spectralPeaksList.Add(new SpectralPeaks(sampleRate, maxSpectralPeaks, 20000, 1, 0, OrderByType.Amplitude));
                this.pitchSalienceList.Add(new PitchSalienceFunction(binResolution, referenceFrequency, magnitudeThreshold, magnitudeCompression, numberHarmonics, harmonicWeight));
                this.saliencePeaksList.Add(new PitchSalienceFunctionPeaks(binResolution, 1, 20000, referenceFrequency));
                this.semaphoreList.Add(new SemaphoreSlim(1, 1));
            }
        }

        /// <summary>
        /// Compute the melody given the audio signal.
        /// </summary>
        /// <param name="signal">The input audio signal.</param>
        /// <returns>
        /// pitch = The frequency carrying the melody at each frame.
        /// pitchConfidence = the confidence in the melody estimation at each frame.
        /// </returns>
        public (float[] pitch, float[] pitchConfidence) Compute(float[] signal)
        {
            this.frameCutter.SetBuffer(signal);
            List<float[]> peakBins = new List<float[]>();
            List<float[]> peakSalience = new List<float[]>();
            while(true)
            {
                float[] frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }
                this.windower.Compute(ref frame);
                float[] spectrum = Spectrum.ComputeMagnitudeSpectrum(frame);
                (float[] frequencies, float[] magnitudes) = this.spectralPeaks.Compute(spectrum);
                float[] salience = this.pitchSalience.Compute(frequencies, magnitudes);
                (float[] salienceBins, float[] salienceValues) = this.saliencePeaks.Compute(salience);
                peakBins.Add(salienceBins);
                peakSalience.Add(salienceValues);
            }

            (List<float[]> contoursBins, List<float[]> contoursSalience, float[] contoursStartTimes, float duration) = this.pitchContours.Compute(peakBins, peakSalience);
            return this.melodyDetector.Compute(contoursBins, contoursSalience, contoursStartTimes, duration);
        }

        /// <summary>
        /// Compute the melody slightly faster by doing the peak salience concurrently,
        /// and then computing the contours and finally the melody.
        /// </summary>
        /// <param name="signal">
        /// The input audio signal.
        /// </param>
        /// <returns>
        /// pitch = The frequency carrying the melody at each frame.
        /// pitchConfidence = the confidence in the melody estimation at each frame.
        /// </returns>
        public async Task<(float[] pitch, float[] pitchConfidence)> ComputeAsync(float[] signal)
        {
            this.frameCutter.SetBuffer(signal);
            List<float[]> peakBins = new List<float[]>();
            List<float[]> peakSalience = new List<float[]>();
            int frameIndex = 0;
            SemaphoreSlim semaphore = new SemaphoreSlim(this.semaphoreList.Count, this.semaphoreList.Count);
            List<Task> tasks = new List<Task>();
            while (true)
            {
                float[] frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                await semaphore.WaitAsync();
                tasks.Add(Task.Run(() => ProcessFrame(frame, frameIndex)).ContinueWith((t) => semaphore.Release()));

                frameIndex++;
            }

            await Task.WhenAll(tasks);

            // stuff dictionaries into peakBins/peakSalience
            peakBins.AddRange(this.peakBinsDict.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));
            peakSalience.AddRange(this.peakSalienceDict.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));

            (List<float[]> contoursBins, List<float[]> contoursSalience, float[] contoursStartTimes, float duration) = this.pitchContours.Compute(peakBins, peakSalience);
            return this.melodyDetector.Compute(contoursBins, contoursSalience, contoursStartTimes, duration);
        }

        /// <summary>
        /// Process a single frame of the input signal and stuff the results in a dictionary.
        /// </summary>
        /// <param name="frame">
        /// The frame that should be processed.
        /// </param>
        /// <param name="frameIndex">
        /// The index of the frame being processed.
        /// </param>
        public void ProcessFrame(float[] frame, int frameIndex)
        {
            int i = frameIndex % this.semaphoreList.Count;
            this.semaphoreList[i].Wait();
            this.windowerList[i].Compute(ref frame);
            float[] spectrum = Spectrum.ComputeMagnitudeSpectrum(frame);
            (float[] frequencies, float[] magnitudes) = this.spectralPeaksList[i].Compute(spectrum);
            float[] salience = this.pitchSalienceList[i].Compute(frequencies, magnitudes);
            (float[] salienceBins, float[] salienceValues) = this.saliencePeaksList[i].Compute(salience);
            this.peakBinsDict[frameIndex] = salienceBins;
            this.peakSalienceDict[frameIndex] = salienceValues;
            this.semaphoreList[i].Release();
        }
    }
}
