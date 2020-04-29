using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public enum HFCTechnique
    {
        Masri,
        Jensen,
        Brossier
    }

    public class HighFrequencyContent
    {
        private HFCTechnique type;
        private int sampleRate;
        private double cutoffFrequency;

        // 7040 == A8 which is on the upper bound of useful musical notes.
        public HighFrequencyContent(HFCTechnique type, int sampleRate, double cutoffFrequency = 7040)
        {
            this.type = type;
            this.sampleRate = sampleRate;
            this.cutoffFrequency = cutoffFrequency;
        }

        public float Compute(float[] spectrum)
        {
            float hertzPerBin = 0;
            if(spectrum.Length > 1)
            {
                hertzPerBin = (this.sampleRate / 2.0f) / (spectrum.Length - 1f);
            }

            int maxBin = (int)(this.cutoffFrequency / hertzPerBin);
            float hfc = 0f;
            switch(this.type)
            {
                case HFCTechnique.Masri:
                    for(int i = 0; i < spectrum.Length && i <= maxBin; i++)
                    {
                        hfc += i * hertzPerBin * spectrum[i] * spectrum[i];
                    }
                    break;
                case HFCTechnique.Jensen:
                    for (int i = 0; i < spectrum.Length && i <= maxBin; i++)
                    {
                        hfc += i * hertzPerBin * i * hertzPerBin * spectrum[i];
                    }
                    break;
                case HFCTechnique.Brossier:
                default:
                    for (int i = 0; i < spectrum.Length && i <= maxBin; i++)
                    {
                        hfc += i * hertzPerBin * spectrum[i];
                    }
                    break;
            }

            return hfc;
        }
    }
}
