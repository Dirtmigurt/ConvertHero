namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

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
            this.Windower = new Windowing(WindowingType.Hann);
            this.OnsetDetectors = new SimpleOnsetDetectors((int)this.sampleRate);
            this.RMSTicks = new TempoTapDegara(this.sampleRate / this.hopSize, Resample.X2, maxTempo, minTempo);
            this.ComplexTicks = new TempoTapDegara(this.sampleRate / this.hopSize, Resample.X2, maxTempo, minTempo);
            this.MelFluxTicks = new TempoTapDegara(this.sampleRate / this.hopSize, Resample.X2, maxTempo, minTempo);

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
            ReportProgress?.Invoke($"Placing Beat Positions using Beat Emphasis Features", 88);
            List<float> beatEmphasisTicks = averager.Compute(t2.Result).ToList();

            // create a List<List<float>> list of the tick arrays and pass that into the TempoTapMaxAgreement Algo
            ReportProgress?.Invoke($"Choosing beat positions to minimize entropy", 95);
            List<List<float>> onsetTicks = new List<List<float>> { rmsTicks, complexTicks, melFluxTicks, infogainTicks, beatEmphasisTicks };
            return this.TempoMaxAgreementSelector.Compute(onsetTicks);
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

            // create a List<List<float>> list of the tick arrays and pass that into the TempoTapMaxAgreement Algo
            return new List<float[]> { rmsTicks, complexTicks, melFluxTicks, infogainTicks, beatEmphasisTicks };

        }
    }
}
