namespace ConvertHero.AudioFileHelpers
{
    using MathNet.Numerics;

    /// <summary>
    /// Class with helper functions to comput Fourier Operations.
    /// </summary>
    public static class Spectrum
    {
        /// <summary>
        /// Compute the FFT of a signal and return the Magnitude spectrum
        /// </summary>
        /// <param name="signal"></param>
        /// <returns></returns>
        public static float[] ComputeMagnitudeSpectrum(float[] signal)
        {
            Complex32[] fft = new Complex32[signal.Length];
            for(int i = 0; i < signal.Length; i++)
            {
                fft[i] = signal[i];
            }

            // compute the FFT
            MathNet.Numerics.IntegralTransforms.Fourier.Forward(fft);

            // Take the magnitude of the FFT
            float[] magnitudeSpectrum = new float[(fft.Length / 2) + 1];
            for(int i = 0; i < magnitudeSpectrum.Length; i++)
            {
                magnitudeSpectrum[i] = fft[i].Magnitude;
            }

            return magnitudeSpectrum;
        }

        /// <summary>
        /// Compute the FFT of a signal and return the Complex spectrum as well as a copy of the Magnitudes spectrum
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="sampleRate"></param>
        /// <returns></returns>
        public static (Complex32[], float[]) ComputeFFTWithMagnitude(float[] signal)
        {
            Complex32[] fft = new Complex32[signal.Length];
            for (int i = 0; i < signal.Length; i++)
            {
                fft[i] = signal[i];
            }

            // compute the FFT
            MathNet.Numerics.IntegralTransforms.Fourier.Forward(fft);

            // Take the magnitude of the FFT
            float[] magnitudeSpectrum = new float[(fft.Length / 2) + 1];
            for (int i = 0; i < magnitudeSpectrum.Length; i++)
            {
                magnitudeSpectrum[i] = fft[i].Magnitude;
            }

            return (fft, magnitudeSpectrum);
        }

        /// <summary>
        /// Compute the FFT of a signal.
        /// </summary>
        /// <param name="signal"></param>
        /// <returns></returns>
        public static Complex32[] ComputeFFT(float[] signal)
        {
            Complex32[] fft = new Complex32[signal.Length];
            for (int i = 0; i < signal.Length; i++)
            {
                fft[i] = signal[i];
            }

            // compute the FFT
            MathNet.Numerics.IntegralTransforms.Fourier.Forward(fft);

            return fft;
        }

        /// <summary>
        /// Compute the Inverse FFT of a spectrum.
        /// </summary>
        /// <param name="fft">
        /// The input spectrum
        /// </param>
        /// <param name="normalize">
        /// Should the output signal be normalized?
        /// </param>
        /// <returns>
        /// </returns>
        public static float[] ComputeIFFT(Complex32[] fft, bool normalize = false)
        {
            Complex32[] temp = new Complex32[fft.Length];
            for(int i = 0; i < fft.Length; i++)
            {
                temp[i] = new Complex32(fft[i].Real, fft[i].Imaginary);
            }

            MathNet.Numerics.IntegralTransforms.Fourier.Inverse(temp);
            float[] signal = new float[temp.Length];
            if (normalize)
            {
                for (int i = 0; i < signal.Length; i++)
                {
                    signal[i] = temp[i].Magnitude / temp.Length;
                }
            }
            else
            {
                for (int i = 0; i < signal.Length; i++)
                {
                    signal[i] = temp[i].Magnitude;
                }
            }

            return signal;
        }
    }
}
