namespace ConvertHero.AudioFileHelpers
{
    using System;

    /// <summary>
    /// What type of normalization are supported.
    /// </summary>
    public enum FluxNormalizationMethod
    {
        L1,
        L2
    }

    /// <summary>
    /// Class that allows computing the flux between subsequent frames of a spectrum.
    /// </summary>
    public class Flux
    {
        /// <summary>
        /// What type of normalization should be used on the output.
        /// </summary>
        private FluxNormalizationMethod normalizeMethod;

        /// <summary>
        /// HalfRectify == true means only frequency magnitudes that increase (onset) contribute to the spectral flux instead of magnitudes that decrese (offsets)
        /// </summary>
        private bool halfRectify;

        /// <summary>
        /// Holds the previous frame which is used to compute the flux between the current and this as previous.
        /// </summary>
        private float[] previousSpectrum = null;

        /// <summary>
        /// Initializes a new instance of the Flux class.
        /// </summary>
        /// <param name="norm">
        /// The type of normalization.
        /// </param>
        /// <param name="halfRectify">
        /// Should the output bet half-rectified (ignore negative values).
        /// </param>
        public Flux(FluxNormalizationMethod norm, bool halfRectify)
        {
            this.normalizeMethod = norm;
            this.halfRectify = halfRectify;
        }

        /// <summary>
        /// Compute the spectral flux between the input spectrum and the one provided in the previous call to this function.
        /// </summary>
        /// <param name="spectrum">
        /// The new input spectrum.
        /// </param>
        /// <returns>
        /// The flux between the new and previous spectrum.
        /// </returns>
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
