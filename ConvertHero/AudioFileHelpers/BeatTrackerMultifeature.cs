using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConvertHero.AudioFileHelpers
{
    public class BeatTrackerMultifeature
    {
        private float sampleRate;
        private int frameSize;
        private int hopSize;

        FrameCutter FrameCutter;
        Windowing Windower;
        SimpleOnsetDetectors OnsetDetectors;
        OnsetDetectionGlobal InfogainDetection;
        OnsetDetectionGlobal BeatEmphasisDetection;
        TempoTapDegara RMSTicks;
        TempoTapDegara ComplexTicks;
        TempoTapDegara MelFluxTicks;
        TempoTapDegara InfogainTicks;
        TempoTapDegara BeatEmphasisTicks;
        TempoTapMaxAgreement TempoMaxAgreementSelector;

        //TempoTapMagAgreement TempoTapMaxAgreement;

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


        public (float confidence, float[] ticks) Compute(float[] signal)
        {
            // put the signal through allllll of the onset detection->tick pipelines
            Task<float[]> t1 = Task.Run(() => this.InfogainTicks.Compute(this.InfogainDetection.Compute(signal)));
            Task<float[]> t2 = Task.Run(() => this.BeatEmphasisTicks.Compute(this.BeatEmphasisDetection.Compute(signal)));

            this.FrameCutter = new FrameCutter(signal, this.frameSize, this.hopSize, startFromZero: true);
            List<float> rmsOnsets = new List<float>();
            List<float> complexOnsets = new List<float>();
            List<float> melFluxOnsets = new List<float>();
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
            }

            // Smooth all of the ticks since they can get pretty jumpy
            MovingAverage averager = new MovingAverage(5);
            List<float> rmsTicks = averager.Compute(this.RMSTicks.Compute(rmsOnsets.ToArray())).ToList();
            List<float> complexTicks = averager.Compute(this.ComplexTicks.Compute(complexOnsets.ToArray())).ToList();
            List<float> melFluxTicks = averager.Compute(this.MelFluxTicks.Compute(melFluxOnsets.ToArray())).ToList();
            List<float> infogainTicks = averager.Compute(t1.Result).ToList();
            List<float> beatEmphasisTicks = averager.Compute(t2.Result).ToList();

            // create a List<List<float>> list of the tick arrays and pass that into the TempoTapMaxAgreement Algo

            List<List<float>> onsetTicks = new List<List<float>> { rmsTicks, complexTicks, melFluxTicks, infogainTicks, beatEmphasisTicks };
            return this.TempoMaxAgreementSelector.Compute(onsetTicks);
        }

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
