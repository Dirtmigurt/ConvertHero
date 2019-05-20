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

        public HighFrequencyContent(HFCTechnique type, int sampleRate)
        {
            this.type = type;
            this.sampleRate = sampleRate;
        }

        public float Compute(float[] spectrum)
        {
            float binToHertz = 0;
            if(spectrum.Length > 1)
            {
                binToHertz = (this.sampleRate / 2.0f) / (spectrum.Length - 1f);
            }

            float hfc = 0f;
            switch(this.type)
            {
                case HFCTechnique.Masri:
                    for(int i = 0; i < spectrum.Length; i++)
                    {
                        hfc += i * binToHertz * spectrum[i] * spectrum[i];
                    }
                    break;
                case HFCTechnique.Jensen:
                    for (int i = 0; i < spectrum.Length; i++)
                    {
                        hfc += i * binToHertz * i * binToHertz * spectrum[i];
                    }
                    break;
                case HFCTechnique.Brossier:
                default:
                    for (int i = 0; i < spectrum.Length; i++)
                    {
                        hfc += i * binToHertz * spectrum[i];
                    }
                    break;
            }

            return hfc;
        }
    }
}
