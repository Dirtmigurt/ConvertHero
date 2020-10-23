using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class PredominantPitchMelodia
    {
        ConcurrentDictionary<int, float[]> peakBinsDict = new ConcurrentDictionary<int, float[]>();
        ConcurrentDictionary<int, float[]> peakSalienceDict = new ConcurrentDictionary<int, float[]>();

        FrameCutter frameCutter;

        Windowing windower;
        // FFT
        SpectralPeaks spectralPeaks;
        PitchSalienceFunction pitchSalience;
        PitchSalienceFunctionPeaks saliencePeaks;
        PitchContours pitchContours;
        PitchContoursMelody melodyDetector;

        #region
        List<Windowing> windowerList = new List<Windowing>();
        // FFT
        List<SpectralPeaks> spectralPeaksList = new List<SpectralPeaks>();
        List<PitchSalienceFunction> pitchSalienceList = new List<PitchSalienceFunction>();
        List<PitchSalienceFunctionPeaks> saliencePeaksList = new List<PitchSalienceFunctionPeaks>();
        List<SemaphoreSlim> semaphoreList = new List<SemaphoreSlim>();
        #endregion

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
