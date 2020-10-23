using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public enum OnsetAlgorithms
    {
        HighFrequencyContent,
        Complex,
        ComplexPhase,
        Flux,
        MelFlux,
        Rms
    }

    public class SimpleOnsetDetectors
    {
        private float sampleRate;

        private bool firstFrame = true;

        // USED FOR RMS ONSET DETECTION
        private bool firstRmsFrame = true;
        private float previousRms = 0;

        // USED FOR COMPLEX PHASE ONSET DETECTION
        private float[] phaseMinus1 = null;
        private float[] phaseMinus2 = null;

        // USED FOR COMPLEX DOMAIN DETECTION
        private float[] complexMinus1 = null;
        private float[] complexMinus2 = null;
        private float[] magMinus1 = null;

        HighFrequencyContent hfc;

        Flux flux;
        Flux melFlux;
        MelBands melBands;
        
        public SimpleOnsetDetectors(int sampleRate)
        {
            this.hfc = new HighFrequencyContent(HFCTechnique.Brossier, sampleRate);
            this.melBands = new MelBands(72, sampleRate, lowFrequencyBound:55, highFrequencyBound:3520);
            this.flux = new Flux(FluxNormalizationMethod.L1, false);
            this.melFlux = new Flux(FluxNormalizationMethod.L1, true);
        }

        public float ComputeHFC(float[] spectrum)
        {
            return this.hfc.Compute(spectrum);
        }

        public float ComputeComplexPhase(float[] magnitude, float[] phase)
        {
            if (this.phaseMinus1 == null || this.phaseMinus2 == null || phase.Length != this.phaseMinus1.Length || phase.Length != phaseMinus2.Length)
            {
                this.phaseMinus1 = new float[phase.Length];
                this.phaseMinus2 = new float[phase.Length];
            }

            float val = 0;
            for(int i = 0; i < phase.Length; i++)
            {
                float targetPhase = 2 * this.phaseMinus1[i] + this.phaseMinus2[i];
                float distance = 2f * magnitude[i] * (float)Math.Sin((phase[i] - targetPhase) / 2f);
                val += (distance * distance);
            }

            this.phaseMinus2 = this.phaseMinus1;
            this.phaseMinus1 = phase;
            return val;
        }

        public float ComputeComplex(float[] magnitude, float[] phase)
        {
            if (this.complexMinus1 == null || this.complexMinus2 == null || phase.Length != this.complexMinus1.Length || phase.Length != this.complexMinus2.Length)
            {
                this.complexMinus1 = new float[phase.Length];
                this.complexMinus2 = new float[phase.Length];
                this.magMinus1 = new float[phase.Length];
            }

            float val = 0;
            for(int i = 0; i < phase.Length; i++)
            {
                float targetPhase = 2 * this.complexMinus1[i] - this.complexMinus2[i];
                targetPhase = (float)(((targetPhase + Math.PI) % (-2 * Math.PI)) + Math.PI);
                float distance = (this.magMinus1[i] - Complex32.FromPolarCoordinates(magnitude[i], phase[i] - targetPhase)).Magnitude;
                val += distance;
            }

            this.complexMinus2 = this.complexMinus1;
            this.complexMinus1 = phase;
            this.magMinus1 = magnitude;
            return val;
        }

        public float ComputeFlux(float[] spectrum)
        {
            return this.flux.Compute(spectrum);
        }

        public float ComputeMelFlux(float[] spectrum)
        {
            float[] bands = this.melBands.Compute(spectrum);
            for(int i = 0; i < bands.Length; i++)
            {
                bands[i] = MathHelpers.AmplitudeToDecibel(bands[i]);
            }

            float val = this.melFlux.Compute(bands);
            if (this.firstFrame)
            {
                // Kill the onset detection that always occurs on the first frame.
                val = 0;
                this.firstFrame = false;
            }

            return val;
        }

        public float ComputeRms(float[] spectrum)
        {
            float rms = 0;
            for(int i = 0; i < spectrum.Length; i++)
            {
                rms += spectrum[i] * spectrum[i];
            }

            float val = 0;
            rms = (float)(Math.Sqrt(rms) / spectrum.Length);
            if (this.firstRmsFrame)
            {
                this.firstRmsFrame = false;
            }
            else
            {
                val = rms - this.previousRms;
                if (val < 0)
                {
                    val = 0;
                }
            }

            this.previousRms = rms;
            return val;
        }
    }
}
