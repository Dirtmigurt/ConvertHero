using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class Windowing
    {
        private float[] window = null;
        private WindowingType type;
        private int zeroPadding = 0;
        private bool zeroPhase = false;
        private bool normalized = false;

        public Windowing(WindowingType type, int zeroPadding = 0, bool zeroPhase = false, bool normalized = false)
        {
            this.type = type;
            this.zeroPadding = zeroPadding;
            this.zeroPhase = zeroPhase;
            this.normalized = false;
        }

        public void Compute(ref float[] signal)
        {
            if (signal == null || signal.Length == 0)
            {
                throw new ArgumentNullException(nameof(signal));
            }

            if (window?.Length != signal.Length)
            {
                this.GenerateWindow(out this.window, signal.Length, this.type);
            }

            int signalSize = signal.Length;
            int totalSize = signalSize + this.zeroPadding;
            if (this.zeroPadding > 0)
            {
                signal = ResizeSignal(signal, signal.Length + this.zeroPadding);
            }

            int i = 0;
            if(this.zeroPhase)
            {
                // first half of the windowed signal is the
                // second half of the signal with windowing!
                for (int j = signalSize / 2; j < signalSize; j++)
                {
                    signal[i++] = signal[j] * this.window[j];
                }

                // zero padding
                for (int j = 0; j < this.zeroPadding; j++)
                {
                    signal[i++] = 0.0f;
                }

                // second half of the signal
                for (int j = 0; j < signalSize / 2; j++)
                {
                    signal[i++] = signal[j] * this.window[j];
                }
            }
            else
            {
                // windowed signal
                for (int j = 0; j < signalSize; j++)
                {
                    signal[i++] = signal[j] * this.window[j];
                }

                // zero padding
                for (int j = 0; j < this.zeroPadding; j++)
                {
                    signal[i++] = 0.0f;
                }
            }
        }

        private static float[] ResizeSignal(float[] arr, int newSize)
        {
            float[] newArr = new float[newSize];
            for(int i = 0; i < arr.Length; i++)
            {
                newArr[i] = arr[i];
            }

            return newArr;
        }

        public void GenerateWindow(out float[] window, int size, WindowingType type)
        {
            switch(type)
            {
                case WindowingType.Hamming:
                    Hamming(out window, size);
                    break;
                case WindowingType.Hann:
                    Hann(out window, size);
                    break;
                case WindowingType.HannNSGCQ:
                    HannNSGCQ(out window, size);
                    break;
                case WindowingType.Triangular:
                    Triangular(out window, size);
                    break;
                case WindowingType.BlackmanHarris62:
                    BlackmanHarris62(out window, size);
                    break;
                case WindowingType.BlackmanHarris70:
                    BlackmanHarris70(out window, size);
                    break;
                case WindowingType.BlackmanHarris74:
                    BlackmanHarris74(out window, size);
                    break;
                case WindowingType.BlackmanHarris92:
                    BlackmanHarris92(out window, size);
                    break;
                case WindowingType.Square:
                default:
                    Square(out window, size);
                    break;
            }

            if (normalized)
            {
                this.NormalizeWindow();
            }
        }

        private static void Hamming(out float[] window, int size)
        {
            window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = (float)(0.53836 - 0.46164 * Math.Cos((2.0 * Math.PI * i) / (size - 1.0)));
            }
        }

        private static void Hann(out float[] window, int size)
        {
            window = new float[size];
            for(int i = 0; i < size; i++)
            {
                window[i] = (float)(0.5 - 0.5 * Math.Cos((2.0 * Math.PI * i) / (size - 1.0)));
            }
        }

        private static void HannNSGCQ(out float[] window, int size)
        {
            window = new float[size];
            for (int i = 0; i <= size / 2; i++)
            {
                window[i] = (float)(0.5 + 0.5 * Math.Cos(2.0 * Math.PI * i / size));
            }
            for (int i = size / 2 + 1; i < size; i++)
            {
                window[i] = (float)(0.5 + 0.5 * Math.Cos(-2.0 * Math.PI * i / size));
            }
        }

        private static void Triangular(out float[] window, int size)
        {
            window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = (float)(2.0 / size * (size / 2.0 - Math.Abs((i - (size - 1.0) / 2.0))));
            }
        }

        private static void Square(out float[] window, int size)
        {
            window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = 1.0f;
            }
        }

        private static void BlackmanHarris(float[] window, double a0 = 0, double a1 = 0, double a2 = 0, double a3 = 0)
        {
            int size = window.Length;

            double fConst = 2f * Math.PI / (size - 1);

            if (size % 2 != 0)
            {
                window[size / 2] = (float)(a0 - a1 * Math.Cos(fConst * (size / 2.0)) + a2 * Math.Cos(fConst * 2 * (size / 2.0)) - a3 * Math.Cos(fConst * 3 * (size / 2.0)));
            }

            for (int i = 0; i < size / 2; i++)
            {
                window[i] = (float)(a0 - a1 * Math.Cos(fConst * i) + a2 * Math.Cos(fConst * 2 * i) - a3 * Math.Cos(fConst * 3 * i));
                window[size - i - 1] = window[i];
            }
        }

        private static void BlackmanHarris62(out float[] window, int size)
        {
            window = new float[size];
            BlackmanHarris(window, 0.44959, 0.49364, 0.05677);
        }

        private void BlackmanHarris70(out float[] window, int size)
        {
            window = new float[size];
            BlackmanHarris(window, 0.42323, 0.49755, 0.07922);
        }

        private void BlackmanHarris74(out float[] window, int size)
        {
            window = new float[size];
            BlackmanHarris(window, 0.40217, 0.49703, 0.09892, 0.00188);
        }

        private void BlackmanHarris92(out float[] window, int size)
        {
            window = new float[size];
            BlackmanHarris(window, 0.35875, 0.48829, 0.14128, 0.01168);
        }

        private void NormalizeWindow()
        {
            float sum = 0f;
            for(int i = 0; i < this.window.Length; i++)
            {
                sum += Math.Abs(this.window[i]);
            }

            if (sum == 0)
            {
                return;
            }

            float scale = 2f / sum;
            for(int i = 0; i < this.window.Length; i++)
            {
                this.window[i] *= scale;
            }
        }
    }

    public enum WindowingType
    {
        Hamming,
        Hann,
        HannNSGCQ,
        Triangular,
        Square,
        BlackmanHarris62,
        BlackmanHarris70,
        BlackmanHarris74,
        BlackmanHarris92
    }
}
