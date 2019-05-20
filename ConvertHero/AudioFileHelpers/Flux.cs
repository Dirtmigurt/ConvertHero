using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public enum FluxNormalizationMethod
    {
        L1,
        L2
    }

    public class Flux
    {
        private FluxNormalizationMethod normalizeMethod;
        private bool halfRectify;
        private float[] previousSpectrum = null;

        public Flux(FluxNormalizationMethod norm, bool halfRectify)
        {
            this.normalizeMethod = norm;
            this.halfRectify = halfRectify;
        }

        public float Compute(float[] spectrum)
        {
            float flux = 0f;

            if (this.previousSpectrum == null)
            {
                this.previousSpectrum = new float[spectrum.Length];
            }

            if (this.normalizeMethod == FluxNormalizationMethod.L1 && !this.halfRectify)
            {
                for (int i = 0; i < spectrum.Length; i++)
                {
                    flux += Math.Abs(spectrum[i] - this.previousSpectrum[i]);
                }
            }
            else if (this.normalizeMethod == FluxNormalizationMethod.L2 && !this.halfRectify)
            {
                for (int i = 0; i < spectrum.Length; i++)
                {
                    flux += (spectrum[i] - this.previousSpectrum[i]) * (spectrum[i] - this.previousSpectrum[i]);
                }

                flux = (float)Math.Sqrt(flux);
            }
            else if (this.normalizeMethod == FluxNormalizationMethod.L1 && this.halfRectify)
            {
                for (int i = 0; i < spectrum.Length; i++)
                {
                    float diff = spectrum[i] - this.previousSpectrum[i];
                    if (diff > 0)
                    {
                        flux += diff; ;
                    }
                }
            }
            else if (this.normalizeMethod == FluxNormalizationMethod.L2 && this.halfRectify)
            {
                for (int i = 0; i < spectrum.Length; i++)
                {
                    float diff = spectrum[i] - this.previousSpectrum[i];
                    if (diff > 0)
                    {
                        flux += diff * diff;
                    }
                }

                flux = (float)Math.Sqrt(flux);
            }

            this.previousSpectrum = spectrum;
            return flux;
        }
    }
}
