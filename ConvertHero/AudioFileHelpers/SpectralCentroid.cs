using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class SpectralCentroid
    {
        public static float Compute(float[] spectrum, int sampleRate)
        {
            float hertzPerBin = (sampleRate / 2.0f) / (spectrum.Length - 1f);
            float fn = hertzPerBin / 2.0f;
            float numerator = 0;
            float denominator = float.Epsilon;
            for(int i = 0; i < spectrum.Length; i++)
            {
                numerator += spectrum[i] * fn;
                denominator += spectrum[i];

                fn += hertzPerBin;
            }

            return numerator / denominator;
        }
    }
}
