using Accord.Audio;
using MathNet.Numerics;
using NAudio.Wave;
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

        public static (Complex32[], float[]) ComputeFFTWithMagnitude(float[] signal, int sampleRate = 44100)
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

        public static Complex32[] ComputeFFT(float[] signal, int sampleRate = 44100)
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
