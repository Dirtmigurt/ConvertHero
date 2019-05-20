using System;
using System.Collections.Generic;
using System.Linq;
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

        public Windowing(WindowingType type, int zeroPadding = 0, bool zeroPhase = false)
        {
            this.type = type;
            this.zeroPadding = zeroPadding;
            this.zeroPhase = zeroPhase;
        }

        public void Compute(ref float[] signal)
        {
            if (signal == null || signal.Length == 0)
            {
                throw new ArgumentNullException(nameof(signal));
            }

            if (window?.Length != signal.Length)
            {
                GenerateWindow(out this.window, signal.Length, this.type);
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

        public static void GenerateWindow(out float[] window, int size, WindowingType type)
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
                case WindowingType.Square:
                default:
                    Square(out window, size);
                    break;
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
    }

    public enum WindowingType
    {
        Hamming,
        Hann,
        HannNSGCQ,
        Triangular,
        Square
    }
}
