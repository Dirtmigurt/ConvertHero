namespace ConvertHero.AudioFileHelpers
{
    using Accord.Audio;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Some test methods using FFT
    /// </summary>
    public static class SignalProcessing
    {
        /// <summary>
        /// Compute the MelPowerSpectrum of an audio file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to compute the spectrum for.
        /// </param>
        /// <returns>
        /// The MelPowerSpectrum.
        /// </returns>
        public static float[,] GetMelPowerSpectrogram(string fileName)
        {
            List<float> sampleList = new List<float>();
            int sampleRate = -1;
            List<float[]> notePowerTimeSeries = new List<float[]>();
            using (SampleReader reader = new SampleReader(fileName, 50, (int)Math.Pow(2, 12)))
            {
                sampleRate = reader.SampleRate;
                float[] buffer;
                int x = reader.Read(out buffer);
                while (x == Math.Pow(2, 12))
                {
                    //sampleList.AddRange(buffer);

                    byte[] bytes = new byte[x * sizeof(float)];
                    Buffer.BlockCopy(buffer.ToArray(), 0, bytes, 0, bytes.Length);
                    Signal s = new Signal(bytes, 1, x, sampleRate, SampleFormat.Format32BitIeeeFloat);
                    ComplexSignal csignal = ComplexSignal.FromSignal(s);
                    csignal.ForwardFourierTransform();

                    System.Numerics.Complex[] channel = csignal.GetChannel(0);
                    double[] freqv = Accord.Audio.Tools.GetFrequencyVector(csignal.Length, csignal.SampleRate);
                    notePowerTimeSeries.Add(GetNotePower(Tools.GetPowerSpectrum(channel), freqv[1]));
                    //this.SpectrogramData.Add(power.ToList());
                    x = reader.Read(out buffer);
                }

                double max = notePowerTimeSeries.Select(col => col.Max()).Where(d => !double.IsPositiveInfinity(d)).Max();
                double scale = 100 / max;
                float[,] result = new float[notePowerTimeSeries.Count, notePowerTimeSeries[0].Length];
                for (int frame = 0; frame < notePowerTimeSeries.Count; frame++)
                {
                    for(int tone = 0; tone < notePowerTimeSeries[frame].Length; tone++)
                    {
                        result[frame, tone] = notePowerTimeSeries[frame][tone] > 0 ? (float)Math.Log(Math.Log(2 + tone) / notePowerTimeSeries[frame][tone]) : 0;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Converts a power spectrum to note powers.
        /// </summary>
        /// <param name="fftPowerSpectrum"></param>
        /// <param name="bandwidth"></param>
        /// <returns></returns>
        private static float[] GetNotePower(double[] fftPowerSpectrum, double bandwidth)
        {
            // A4 = 440 HZ
            int a4 = 440;
            double a = Math.Pow(2, 1.0 / 12.0);
            int octaves = 3;
            float[] notePower = new float[(2 * octaves * 12) + 1];
            int note = 0;
            for (int i = -12 * octaves; i <= octaves * 12; i++)
            {
                double f = a4 * Math.Pow(a, i);
                double fmin = ((a4 * Math.Pow(a, i - 1)) + f) / 2.0;
                double fmax = ((a4 * Math.Pow(a, i + 1)) + f) / 2.0;

                // which bucket of the power spectrum does min and max fall into?
                int mindex = (int)(fmin / bandwidth);
                int maxdex = (int)(fmax / bandwidth);

                // span of the frequencies with infinite precision
                double fwidth = fmax - fmin;

                // span of the frequencies using bandwidth edges
                double fdigital = (maxdex - mindex + 1) * bandwidth;
                double scalar = fwidth / fdigital;

                double powerSum = 0;
                for (int j = mindex; j <= maxdex; j++)
                {
                    powerSum += fftPowerSpectrum[j];
                }

                // Take the log of the power spectruc to get the cepstral mel frequency power spectrum
                //double logPower = Math.Log(powerSum * scalar);

                // If no log is applied this is just the mel frequency power spectrum
                notePower[note++] = (float)(powerSum * scalar);
            }

            return notePower;
        }
    }
}
