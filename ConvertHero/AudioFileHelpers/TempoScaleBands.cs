using MathNet.Numerics;
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class TempoScaleBands
    {
        private float frameFactor;
        private float[] oldBands;
        private float[] bandsGain;


        public TempoScaleBands(int frameTime, float[] bandsGain = null)
        {
            this.frameFactor = (float)Math.Sqrt(256f / frameTime);
            if (bandsGain == null)
            {
                this.bandsGain = new float[] { 2.0f, 3.0f, 2.0f, 1.0f, 1.2f, 2.0f, 3.0f, 2.5f };
            }
            else
            {
                this.bandsGain = bandsGain;
            }
        }

        public (float[] scaledBands, float cumulBands) Compute(float[] bands)
        {
            int size = bands.Length;
            if (size < 1)
            {
                throw new Exception($"TempoScaleBands: a power spectrum should have at least 1 band.");
            }

            if (this.bandsGain.Length != size)
            {
                throw new Exception($"TempoScaleBands: bandsGain and input spectrum bands have different sizes");
            }

            float[] scaledBands = new float[size];
            float[] tempBands = new float[size];
            if (this.oldBands == null || this.oldBands.Length != size)
            {
                this.oldBands = new float[size];
            }

            for(int i = 0; i < size; i++)
            {
                scaledBands[i] = (float)(Math.Log10(1.0 + (100.0 * bands[i])) / Math.Log10(101.0));
            }

            float cumulative = 0f;
            for(int i = 0; i < size; i++)
            {
                tempBands[i] = Math.Max(0f, scaledBands[i] - this.oldBands[i]) * this.frameFactor;
                cumulative += tempBands[i];
            }

            cumulative = this.Scale(cumulative, 0.2f, 1.2f, 0.3f);

            for(int i = 0; i < size; i++)
            {
                this.oldBands[i] = scaledBands[i];
                scaledBands[i] = this.Scale(tempBands[i], 0.1f, 0.5f, 0.4f);
                scaledBands[i] *= this.bandsGain[i];
            }

            return (scaledBands, cumulative);
        }

        /// <summary>
        /// Scalve value 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="pwr"></param>
        /// <returns></returns>
        public float Scale(float value, float c1, float c2, float pwr)
        {
            if (value > c2)
            {
                return (float)(c2 + 0.1 * Math.Log10(value / c2));
            }

            if (value > c1)
            {
                return (float)(c2 + (c2 - c1) * Math.Pow((value - c1) / (c2 - c1), pwr));
            }

            return value;
        }
    }
}
