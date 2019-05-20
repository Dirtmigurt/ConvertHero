using Accord.Audio;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class Spectrum
    {
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

        public static Complex32[] ComputeFFT(float[] signal, int sampleRate = 44100)
        {
            Complex32[] fft = new Complex32[signal.Length];
            for (int i = 0; i < signal.Length; i++)
            {
                fft[i] = signal[i];
            }

            double step = (double)sampleRate / signal.Length;
            int kickBin = (int)(114 / step);
            int snareBin = (int)(218 / step);

            // compute the FFT
            MathNet.Numerics.IntegralTransforms.Fourier.Forward(fft);
            fft[kickBin] /= 2;
            fft[snareBin] /= 2;
            return fft;
        }
    }
}
