using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public static class CartesianToPolar
    {
        public static (float[] Magnitude, float[] Phase) ConvertComplexToPolar(Complex32[] complexSignal, bool ignoreDrums = true)
        {

            float[] Magnitude = new float[complexSignal.Length / 2];
            float[] Phase = new float[complexSignal.Length / 2];

            for(int i = 0; i < complexSignal.Length / 2; i++)
            {
                Magnitude[i] = complexSignal[i].Magnitude;
                Phase[i] = complexSignal[i].Phase;
            }

            return (Magnitude, Phase);
        }
    }
}
