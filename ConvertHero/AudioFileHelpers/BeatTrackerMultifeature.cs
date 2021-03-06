﻿namespace ConvertHero.AudioFileHelpers
{
    using MathNet.Numerics;
    using NAudio.Wave;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Media.Media3D;

    public class BeatTrackerMultifeature
    {
        /// <summary>
        /// The sample rate of the signal.
        /// </summary>
        private float sampleRate;

        /// <summary>
        /// The number of samples that make up a single frame of the signal.
        /// </summary>
        private int frameSize;

        /// <summary>
        /// The number of samples that are advanced between frames.
        /// </summary>
        private int hopSize;

        /// <summary>
        /// The minimum tempo to return.
        /// </summary>
        private float minTempo;

        /// <summary>
        /// The maximum tempo to return.
        /// </summary>
        private float maxTempo;

        /// <summary>
        /// A Helper object for cutting a signal up and returning frames.
        /// </summary>
        FrameCutter FrameCutter;

        /// <summary>
        /// A Helper object for applying common windowing functions to a signal.
        /// </summary>
        Windowing Windower;

        /// <summary>
        /// A Helper object for applying an array of common onset detection algorithms on a signal.
        /// </summary>
        SimpleOnsetDetectors OnsetDetectors;

        /// <summary>
        /// A Helper object for computing the super flux novelty curve of a signal.
        /// </summary>
        SuperFluxExtractor SuperFluxExtractor;

        /// <summary>
        /// A Helper object for applying an infogain beat detection algorithm on a signal.
        /// </summary>
        OnsetDetectionGlobal InfogainDetection;

        /// <summary>
        /// A Helper object for applying a beat emphasis detection algorithm on a signal.
        /// </summary>
        OnsetDetectionGlobal BeatEmphasisDetection;

        /// <summary>
        /// A Helper object for computing the periodicity of onsets detected by the RMS onset detector.
        /// </summary>
        TempoTapDegara RMSTicks;

        /// <summary>
        /// A Helper object for computing the periodicity of onsets detected by the complex onset detector.
        /// </summary>
        TempoTapDegara ComplexTicks;

        /// <summary>
        /// A Helper object for computing the periodicity of onsets detected by the MelFlux onset detector.
        /// </summary>
        TempoTapDegara MelFluxTicks;

        /// <summary>
        /// A Helper object for computing the periodicity of onsets detected by the SuperFlux onset detector.
        /// </summary>
        TempoTapDegara SuperFluxTicks;

        /// <summary>
        /// A Helper object for computing the periodicity of onsets detected by the InfoGain onset detector.
        /// </summary>
        TempoTapDegara InfogainTicks;

        /// <summary>
        /// A Helper object for computing the periodicity of onsets detected by the BeatEmphasis onset detector.
        /// </summary>
        TempoTapDegara BeatEmphasisTicks;

        /// <summary>
        /// A Helper object for selecting which ticks produced by the TempoTapDegara objects match the signal the best.
        /// </summary>
        TempoTapMaxAgreement TempoMaxAgreementSelector;

        /// <summary>
        /// Creates a new instance of the BeatTrackMultiFeature class.
        /// </summary>
        /// <param name="sampleRate">
        /// The sample rate of the input signal.
        /// </param>
        /// <param name="minTempo">
        /// The minimum tempo that the returned beat should be.
        /// </param>
        /// <param name="maxTempo">
        /// The maximum tempo that the returned beat should be.
        /// </param>
        public BeatTrackerMultifeature(float sampleRate, float minTempo = 40, float maxTempo = 240)
        {
            if (sampleRate != 44100)
            {
                throw new Exception($"Detection only supports sample rates of 44100 Hz. Please re-sample the auio signal to meet this requirement.");
            }

            this.sampleRate = sampleRate;
            this.frameSize = 2048;
            this.hopSize = 1024;
            this.minTempo = minTempo;
            this.maxTempo = maxTempo;
            this.Windower = new Windowing(WindowingType.Hann);
            this.OnsetDetectors = new SimpleOnsetDetectors((int)this.sampleRate);
            this.SuperFluxExtractor = new SuperFluxExtractor(sampleRate: this.sampleRate);
            this.RMSTicks = new TempoTapDegara(this.sampleRate / this.hopSize, Resample.X2, maxTempo, minTempo);
            this.ComplexTicks = new TempoTapDegara(this.sampleRate / this.hopSize, Resample.X2, maxTempo, minTempo);
            this.MelFluxTicks = new TempoTapDegara(this.sampleRate / this.hopSize, Resample.X2, maxTempo, minTempo);
            this.SuperFluxTicks = new TempoTapDegara(this.sampleRate / 512, Resample.None, maxTempo, minTempo);

            // Use default frameSize=2048/HopSize=512
            this.InfogainDetection = new OnsetDetectionGlobal(this.sampleRate, OnsetMethod.Infogain);
            this.BeatEmphasisDetection = new OnsetDetectionGlobal(this.sampleRate, OnsetMethod.BeatEmphasis);
            this.InfogainTicks = new TempoTapDegara(maxTempo: maxTempo, minTempo: minTempo);
            this.BeatEmphasisTicks = new TempoTapDegara(maxTempo: maxTempo, minTempo: minTempo);
            this.TempoMaxAgreementSelector = new TempoTapMaxAgreement();
        }

        /// <summary>
        /// Compute the locations of the beat ticks and the confidence in those values.
        /// </summary>
        /// <param name="signal">
        /// The Input signal.
        /// </param>
        /// <param name="ReportProgress">
        /// A Callback function for reporting status to the caller.
        /// </param>
        /// <returns>
        /// locations in time where quarter-note beats occur, and the total confidence in the tempo.
        /// </returns>
        public (float confidence, float[] ticks) Compute(float[] signal, Action<string, double> ReportProgress = null)
        {
            // put the signal through allllll of the onset detection->tick pipelines
            Task<float[]> t1 = Task.Run(() => this.InfogainTicks.Compute(this.InfogainDetection.Compute(signal)));
            Task<float[]> t2 = Task.Run(() => this.BeatEmphasisTicks.Compute(this.BeatEmphasisDetection.Compute(signal)));
            Task<float[]> t3 = Task.Run(() => this.SuperFluxTicks.Compute(this.SuperFluxExtractor.ComputeNovelty(signal)));

            this.FrameCutter = new FrameCutter(signal, this.frameSize, this.hopSize, startFromZero: true);
            List<float> rmsOnsets = new List<float>();
            List<float> complexOnsets = new List<float>();
            List<float> melFluxOnsets = new List<float>();
            double frameEstimate = signal.Length / (double)this.hopSize;
            double lastReportedProgress = -100;
            int currentFrame = 0;
            while (true)
            {
                float[] frame = this.FrameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.Windower.Compute(ref frame);
                (float[] magnitude, float[] phase) = CartesianToPolar.ConvertComplexToPolar(Spectrum.ComputeFFT(frame));

                rmsOnsets.Add(this.OnsetDetectors.ComputeRms(magnitude));
                complexOnsets.Add(this.OnsetDetectors.ComputeComplex(magnitude, phase));
                melFluxOnsets.Add(this.OnsetDetectors.ComputeMelFlux(magnitude));

                // Reduce UI invocations
                double progress = currentFrame / frameEstimate * 50.0;
                if (progress - lastReportedProgress > 0.1)
                {
                    ReportProgress?.Invoke($"Computing Spectral Onset Features", currentFrame / frameEstimate * 50.0 + 1);
                }

                currentFrame++;
            }

            // Smooth all of the ticks since they can get pretty jumpy
            MovingAverage averager = new MovingAverage(5);
            ReportProgress?.Invoke($"Placing Beat Positions using RMS Features", 58);
            List<float> rmsTicks = averager.Compute(this.RMSTicks.Compute(rmsOnsets.ToArray())).ToList();
            ReportProgress?.Invoke($"Placing Beat Positions using Signal Phase Features", 64);
            List<float> complexTicks = averager.Compute(this.ComplexTicks.Compute(complexOnsets.ToArray())).ToList();
            ReportProgress?.Invoke($"Placing Beat Positions using Mel Banded Spectral Flux Features", 72);
            List<float> melFluxTicks = averager.Compute(this.MelFluxTicks.Compute(melFluxOnsets.ToArray())).ToList();
            ReportProgress?.Invoke($"Placing Beat Positions using Signal Information Gain Features", 80);
            List<float> infogainTicks = averager.Compute(t1.Result).ToList();
            ReportProgress?.Invoke($"Placing Beat Positions using SuperFlux Novelty Features", 80);
            List<float> superFluxTicks = averager.Compute(t3.Result).ToList();
            ReportProgress?.Invoke($"Placing Beat Positions using Beat Emphasis Features", 88);
            List<float> beatEmphasisTicks = averager.Compute(t2.Result).ToList();

            // create a List<List<float>> list of the tick arrays and pass that into the TempoTapMaxAgreement Algo
            ReportProgress?.Invoke($"Choosing beat positions to minimize entropy", 95);
            List<List<float>> onsetTicks = new List<List<float>> { rmsTicks, complexTicks, melFluxTicks, superFluxTicks, infogainTicks, beatEmphasisTicks };
            return this.TempoMaxAgreementSelector.Compute(onsetTicks, this.minTempo, this.maxTempo);
        }

        /// <summary>
        /// Compute the locations of the beat ticks and the confidence in those values.
        /// </summary>
        /// <param name="signal">
        /// The Input signal.
        /// </param>
        /// <param name="ReportProgress">
        /// A Callback function for reporting status to the caller.
        /// </param>
        /// <returns>
        /// locations in time where quarter-note beats occur, and the total confidence in the tempo.
        /// </returns>
        public (float confidence, float[] ticks) ComputeCombinations(float[] signal, Action<string, double> ReportProgress = null)
        {
            // put the signal through allllll of the onset detection->tick pipelines
            Task<float[]> t1 = Task.Run(() => this.InfogainDetection.Compute(signal));
            Task<float[]> t2 = Task.Run(() => this.BeatEmphasisDetection.Compute(signal));
            Task<float[]> t3 = Task.Run(() => this.SuperFluxExtractor.ComputeNovelty(signal));

            this.FrameCutter = new FrameCutter(signal, this.frameSize, this.hopSize, startFromZero: true);
            List<float> rmsOnsets = new List<float>();
            List<float> complexOnsets = new List<float>();
            List<float> melFluxOnsets = new List<float>();
            double frameEstimate = signal.Length / (double)this.hopSize;
            double lastReportedProgress = -100;
            int currentFrame = 0;
            while(true)
            {
                float[] frame = this.FrameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.Windower.Compute(ref frame);
                (float[] magnitude, float[] phase) = CartesianToPolar.ConvertComplexToPolar(Spectrum.ComputeFFT(frame));

                rmsOnsets.Add(this.OnsetDetectors.ComputeRms(magnitude));
                complexOnsets.Add(this.OnsetDetectors.ComputeComplex(magnitude, phase));
                melFluxOnsets.Add(this.OnsetDetectors.ComputeMelFlux(magnitude));

                // Reduce UI invocations
                double progress = currentFrame / frameEstimate * 50.0;
                if (progress - lastReportedProgress > 0.1)
                {
                    ReportProgress?.Invoke($"Computing Spectral Onset Features", currentFrame / frameEstimate * 50.0 + 1);
                }

                currentFrame++;
            }

            // Merge the different onset detection methods so we can get the best each method has to offer.
            ReportProgress?.Invoke($"Placing beats based on Spectral Onset Features", 75);
            List<float[]> spectralOnsets = this.CombineOnsetFeatures(new List<float[]> { rmsOnsets.ToArray(), complexOnsets.ToArray(), melFluxOnsets.ToArray() });
            List<List<float>> tickCandidates = this.ParallelComputeTicksForManyODFs(spectralOnsets, this.sampleRate / this.hopSize, Resample.X2);

            ReportProgress?.Invoke($"Placing beats based on SuperFlux novelty Features", 80);
            tickCandidates.AddRange(this.ParallelComputeTicksForManyODFs(new List<float[]> { t3.Result }, this.sampleRate / 512, Resample.None));

            ReportProgress?.Invoke($"Placing beats based on Global Onset Features", 85);
            List<float[]> globalOnsets = this.CombineOnsetFeatures(new List<float[]> { t1.Result, t2.Result });
            tickCandidates.AddRange(this.ParallelComputeTicksForManyODFs(globalOnsets, 44100f / 512f, Resample.None));


            ReportProgress?.Invoke($"Picking the best beat candidate.", 95);
            return this.TempoMaxAgreementSelector.Compute(tickCandidates, this.minTempo, this.maxTempo);
        }

        /// <summary>
        /// Compute all of the ticks but do not select a winner, instead return all tick candidates.
        /// </summary>
        /// <param name="signal">
        /// The input signal.
        /// </param>
        /// <returns>
        /// The tick candidates generated by each onset detection method.
        /// </returns>
        public List<float[]> ComputeAll(float[] signal)
        {
            // put the signal through allllll of the onset detection->tick pipelines
            Task<float[]> t1 = Task.Run(() => this.InfogainTicks.Compute(this.InfogainDetection.Compute(signal)));
            Task<float[]> t2 = Task.Run(() => this.BeatEmphasisTicks.Compute(this.BeatEmphasisDetection.Compute(signal)));
            Task<float[]> t3 = Task.Run(() => this.SuperFluxTicks.Compute(this.SuperFluxExtractor.ComputeNovelty(signal)));

            this.FrameCutter = new FrameCutter(signal, this.frameSize, this.hopSize, startFromZero: true);
            List<float> rmsOnsets = new List<float>();
            List<float> complexOnsets = new List<float>();
            List<float> melFluxOnsets = new List<float>();
            while (true)
            {
                float[] frame = this.FrameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.Windower.Compute(ref frame);
                (float[] magnitude, float[] phase) = CartesianToPolar.ConvertComplexToPolar(Spectrum.ComputeFFT(frame));

                rmsOnsets.Add(this.OnsetDetectors.ComputeRms(magnitude));
                complexOnsets.Add(this.OnsetDetectors.ComputeComplex(magnitude, phase));
                melFluxOnsets.Add(this.OnsetDetectors.ComputeMelFlux(magnitude));
            }

            float[] rmsTicks = this.RMSTicks.Compute(rmsOnsets.ToArray());
            float[] complexTicks = this.ComplexTicks.Compute(complexOnsets.ToArray());
            float[] melFluxTicks = this.MelFluxTicks.Compute(melFluxOnsets.ToArray());
            float[] infogainTicks = t1.Result;
            float[] beatEmphasisTicks = t2.Result;
            float[] superFluxTicks = t3.Result;

            // create a List<List<float>> list of the tick arrays and pass that into the TempoTapMaxAgreement Algo
            return new List<float[]> { rmsTicks, complexTicks, melFluxTicks, superFluxTicks, infogainTicks, beatEmphasisTicks };
        }

        /// <summary>
        /// compute only the onsets, but do not estimate the ticks.
        /// </summary>
        /// <param name="signal">
        /// the input audio signal.
        /// </param>
        /// <returns>
        /// A List of Onset detection functions.
        /// </returns>
        public List<float[]> ComputeOnsets(float[] signal)
        {
            // put the signal through allllll of the onset detection->tick pipelines
            Task<float[]> t1 = Task.Run(() => this.InfogainDetection.Compute(signal));
            Task<float[]> t2 = Task.Run(() => this.BeatEmphasisDetection.Compute(signal));
            Task<float[]> t3 = Task.Run(() => this.SuperFluxExtractor.ComputeNovelty(signal));

            this.FrameCutter = new FrameCutter(signal, this.frameSize, this.hopSize, startFromZero: true);
            List<float> rmsOnsets = new List<float>();
            List<float> complexOnsets = new List<float>();
            List<float> melFluxOnsets = new List<float>();
            while (true)
            {
                float[] frame = this.FrameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                this.Windower.Compute(ref frame);
                (float[] magnitude, float[] phase) = CartesianToPolar.ConvertComplexToPolar(Spectrum.ComputeFFT(frame));

                rmsOnsets.Add(this.OnsetDetectors.ComputeRms(magnitude));
                complexOnsets.Add(this.OnsetDetectors.ComputeComplex(magnitude, phase));
                melFluxOnsets.Add(this.OnsetDetectors.ComputeMelFlux(magnitude));
            }

            // rms/complex/melFlux hopsize=1024, infogain/beatemphasis/superflux hopsize=512
            return new List<float[]> { rmsOnsets.ToArray(), complexOnsets.ToArray(), melFluxOnsets.ToArray(), t3.Result, t1.Result, t2.Result };
        }

        /// <summary>
        /// Compute all possible combinations of onset features.
        /// This means what if we merged the RMS + Complex onsets? What if we merged RMS + Complex + MelFlux? etc.
        /// </summary>
        /// <param name="singleSourceOnsets">
        /// All of the onset detection functions with the same length.
        /// </param>
        /// <returns>
        /// All possible combinations of the onset detection functions.
        /// </returns>
        private List<float[]> CombineOnsetFeatures(List<float[]> singleSourceOnsets)
        {
            // ODFs need to be normalized before being merged, because HFC peaks may occur at magnitudes of 250, while flux peaks may occur at magnitudes of 12.
            List<float[]> normalizedODFs = new List<float[]>();
            for(int i = 0; i < singleSourceOnsets.Count; i++)
            {
                normalizedODFs.Add(MathHelpers.NormalizeClone(singleSourceOnsets[i]));
            }

            List<List<float[]>> combinations = GetAllCombos(normalizedODFs);
            List<float[]> result = new List<float[]>();
            foreach(List<float[]> combo in combinations)
            {
                float[] merged = new float[combo[0].Length];
                for(int i = 0; i < combo.Count; i++)
                {
                    for(int j = 0; j < combo[i].Length; j++)
                    {
                        merged[j] += combo[i][j];
                    }
                }

                result.Add(merged);
            }

            return result;
        }

        /// <summary>
        /// Recursive method to generate all possible unique combinations of the items in the input list.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the object in the input list.
        /// </typeparam>
        /// <param name="list">
        /// The input list to generate combinations of.
        /// </param>
        /// <returns>all possible unique combinations of the items in the input list.</returns>
        private static List<List<T>> GetAllCombos<T>(List<T> list)
        {
            List<List<T>> result = new List<List<T>>();
            // head
            result.Add(new List<T>());
            result.Last().Add(list[0]);
            if (list.Count == 1)
                return result;
            // tail
            List<List<T>> tailCombos = GetAllCombos(list.Skip(1).ToList());
            tailCombos.ForEach(combo =>
            {
                result.Add(new List<T>(combo));
                combo.Add(list[0]);
                result.Add(new List<T>(combo));
            });
            return result;
        }

        /// <summary>
        /// Compute the Beat positions for all of the OnsetDetection functions but attempt each ODF 9 times at different min/max tempo values.
        /// </summary>
        /// <param name="odfs">
        /// The list of Onset Detection function outputs.
        /// </param>
        /// <param name="ODFSampleRate">
        /// The sample rate of the onset detection function.
        /// </param>
        /// <param name="resample">
        /// Shoudl the onset detection function be resampled to increase resolution?
        /// </param>
        /// <returns>
        /// The potential ticks for each ODF at many different tempo preferences from 40-60 to 200-240 BPM.
        /// </returns>
        private List<List<float>> ComputeTicksForManyODFs(List<float[]> odfs, float ODFSampleRate, Resample resample)
        {
            List<List<float>> potentialTicks = new List<List<float>>();
            int tempoStep = 20;
            int tempoWindow = 40;
            MovingAverage averager = new MovingAverage(5);
            foreach (float[] odf in odfs)
            {
                float minutes = odf.Length / ODFSampleRate / 60f;
                for(int tMin = 60; tMin <= 200; tMin += tempoStep)
                {
                    TempoTapDegara tickFinder = new TempoTapDegara(ODFSampleRate, resample, tMin + tempoWindow, tMin);
                    float[] ticks = averager.Compute(tickFinder.Compute(odf));
                    if (ticks.Length / minutes < 250)
                    {
                        potentialTicks.Add(ticks.ToList());
                    }
                }
            }

            return potentialTicks;
        }

        /// <summary>
        /// Compute the Beat positions for all of the OnsetDetection functions but attempt each ODF 9 times at different min/max tempo values.
        /// </summary>
        /// <param name="odfs">
        /// The list of Onset Detection function outputs.
        /// </param>
        /// <param name="ODFSampleRate">
        /// The sample rate of the onset detection function.
        /// </param>
        /// <param name="resample">
        /// Shoudl the onset detection function be resampled to increase resolution?
        /// </param>
        /// <returns>
        /// The potential ticks for each ODF at many different tempo preferences from 40-60 to 200-240 BPM.
        /// </returns>
        private List<List<float>> ParallelComputeTicksForManyODFs(List<float[]> odfs, float ODFSampleRate, Resample resample)
        {
            ConcurrentBag<List<float>> potentialTicks = new ConcurrentBag<List<float>>();
            int tempoStep = 20;
            int tempoWindow = 40;

            Parallel.ForEach(odfs,
                new ParallelOptions { MaxDegreeOfParallelism = 12 },
                (odf) =>
                {
                    MovingAverage averager = new MovingAverage(5);
                    float minutes = odf.Length / ODFSampleRate / 60f;
                    for (int tMin = 60; tMin <= 200; tMin += tempoStep)
                    {
                        TempoTapDegara tickFinder = new TempoTapDegara(ODFSampleRate, resample, tMin + tempoWindow, tMin);
                        float[] ticks = averager.Compute(tickFinder.Compute(odf));
                        if (ticks.Length / minutes < 250)
                        {
                            potentialTicks.Add(ticks.ToList());
                        }
                    }
                });

            return potentialTicks.ToList();
        }
    }
}
