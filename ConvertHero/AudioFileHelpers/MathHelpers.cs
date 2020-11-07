namespace ConvertHero.AudioFileHelpers
{
    using Accord.Math;
    using MathNet.Numerics;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Lots of useful little math functions.
    /// </summary>
    public static class MathHelpers
    {
        /// <summary>
        /// Values below this should be treated as silence
        /// </summary>
        private const float SilenceCutoff = 1e-9f;

        /// <summary>
        /// Values in a spectrogram below this should be treated as silence.
        /// </summary>
        private const float SilenceCutoffDecibel = -90;

        /// <summary>
        /// Convert a frequency in hertz to the Mel scale.
        /// </summary>
        /// <param name="hz">
        /// Input frequency.
        /// </param>
        /// <returns>
        /// The mel frequency.
        /// </returns>
        public static float HertzToMel(float hz)
        {
            return (float)(1127.01048 * Math.Log(hz / 700.0 + 1.0));
        }

        /// <summary>
        /// Convert Mel frequency to Hertz.
        /// </summary>
        /// <param name="mel">
        /// the input mel frequency.
        /// </param>
        /// <returns>
        /// The frequency in Hz.
        /// </returns>
        public static float MelToHertz(float mel)
        {
            return (float)(700.0 * (Math.Exp(mel / 1127.01048) - 1.0));
        }

        /// <summary>
        /// Convert a frequency in hertz to the Mel scale.
        /// </summary>
        /// <param name="hz">
        /// Input frequency.
        /// </param>
        /// <returns>
        /// The mel frequency.
        /// </returns>
        public static float HertzToMel10(float hz)
        {
            return (float)(2595.0 * Math.Log10(hz / 700.0 + 1.0));
        }

        /// <summary>
        /// Convert Mel frequency to Hertz.
        /// </summary>
        /// <param name="mel">
        /// the input mel frequency.
        /// </param>
        /// <returns>
        /// The frequency in Hz.
        /// </returns>
        public static float Mel10ToHertz(float mel)
        {
            return (float)(700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0));
        }

        /// <summary>
        /// Convert Hz to Mel based on Slaney's formula in MATLAB Auditory Toolbox
        /// </summary>
        /// <param name="hz">
        /// The input frequency.
        /// </param>
        /// <returns>
        /// the mel frequency.
        /// </returns>
        public static float HertzToMelSlaney(float hz)
        {
            const float minLogHz = 1000.0f;
            const float linSlope = 3 / 200.0f;

            if (hz < minLogHz)
            {
                // Linear part: 0 - 1000 Hz.
                return hz * linSlope;
            }
            else
            {
                // Log-scale part: >= 1000 Hz.
                float minLogMel = minLogHz * linSlope;
                float logStep = (float)(Math.Log(6.4) / 27.0);
                return (float)(minLogMel + Math.Log(hz / minLogHz) / logStep);
            }
        }

        /// <summary>
        /// Convert Hz to Hz. very complicated stuff.
        /// </summary>
        /// <param name="hz"></param>
        /// <returns></returns>
        public static float HertzToHertz(float hz)
        {
            return hz;
        }

        /// <summary>
        /// Convert hertz to cents.
        /// </summary>
        /// <param name="hz">
        /// The input frequency.
        /// </param>
        /// <returns>
        /// The resulting cents.
        /// </returns>
        public static float HertzToCents(float hz)
        {
            return (float)(12 * Math.Log(hz / 440.0) / Math.Log(2.0) + 69.0);
        }

        /// <summary>
        /// Convert hertz to tone, where A4 = 440Hz, A4 = 48th tone.
        /// </summary>
        /// <param name="hz">
        /// The frequency in Hz of the tone.
        /// </param>
        /// <returns>
        /// Which number tone equals the given frequency.
        /// </returns>
        public static float HertzToTone(float hz)
        {
            if (hz < float.Epsilon)
            {
                return 0;
            }

            return 12f * Log2(hz / 27.5);
        }

        /// <summary>
        /// Compute the bin magnitudes for a spectrum.
        /// </summary>
        /// <param name="fft">
        /// the input spectrum.
        /// </param>
        /// <param name="gain">
        /// The gain in Db.
        /// </param>
        /// <param name="range">
        /// The range in Db.
        /// </param>
        /// <param name="windowCorrectionFactor">
        /// No Windowing = 1.0, Hanning=2.0, Flattop=4.18, Blackman=2.8, Hamming=1.85, Kaiser-Bessel=2.49
        /// </param>
        /// <param name="maxBins">
        /// The maximum number of bins to include.
        /// </param>
        /// <returns>
        /// The magnitudes of each spectrum bin.
        /// </returns>
        public static float[] ComputeBinMagnitudes(Complex32[] fft, int gain = 20, int range = 80, float windowCorrectionFactor = 2.0f, int maxBins = int.MaxValue)
        {
            int fftLength = fft.Length / 2;
            int stop = Math.Min(maxBins, fftLength);
            float[] result = new float[stop];
            
            for(int i = 0; i < stop; i++)
            {
                float preNorm = AmplitudeToDecibel(2 * fft[i].Magnitude / (fftLength / windowCorrectionFactor));

                preNorm = Math.Min(0, preNorm + gain);
                preNorm = Math.Max(-range, preNorm);
                result[i] = ((preNorm + range) / range);
            }

            double m, b;
            double err = FindLinearLeastSquaresFit(result, out m, out b);
            ApplyLinearNormalization(result, m, b);
            return result;
        }

        /// <summary>
        /// Apply a linear normalization to the input points.
        /// </summary>
        /// <param name="points">
        /// The input points.
        /// </param>
        /// <param name="m">
        /// The m value from y = mx + b
        /// </param>
        /// <param name="b">
        /// The b value from y = mx + b
        /// </param>
        private static void ApplyLinearNormalization(float[] points, double m, double b)
        {
            double line = b;
            int i = 0;
            while(line > 0 && i < points.Length - 1)
            {
                line = i * m + b;

                points[i] = Math.Max((float)(points[i] - line), 0);
                i++;
            }
        }

        /// <summary>
        ///  Find the least squares linear fit.
        ///  Return the total error.
        /// </summary>
        /// <param name="points">
        /// The input points
        /// </param>
        /// <param name="m">
        /// The m in y = mx + b
        /// </param>
        /// <param name="b">
        /// The b in y = mx + b
        /// </param>
        /// <returns>
        /// The total error.
        /// </returns>

        public static double FindLinearLeastSquaresFit(float[] points, out double m, out double b)
        {
            // Perform the calculation.
            // Find the values S1, Sx, Sy, Sxx, and Sxy.
            double S1 = points.Length;
            double Sx = 0;
            double Sy = 0;
            double Sxx = 0;
            double Sxy = 0;

            int x = 0;
            foreach (float f in points)
            {
                Sx += x;
                Sy += f;
                Sxx += x * x;
                Sxy += x * f;
                x++;
            }

            // Solve for m and b.
            m = (Sxy * S1 - Sx * Sy) / (Sxx * S1 - Sx * Sx);
            b = (Sxy * Sx - Sy * Sxx) / (Sx * Sx - S1 * Sxx);

            return Math.Sqrt(ErrorSquared(points, m, b));
        }

        /// <summary>
        /// Get the error squared.
        /// </summary>
        /// <param name="points">
        /// The input points
        /// </param>
        /// <param name="m">
        /// The m in y = mx + b
        /// </param>
        /// <param name="b">
        /// The b in y = mx + b
        /// </param>
        /// <returns>
        /// The error squared.
        /// </returns>
        public static double ErrorSquared(float[] points, double m, double b)
        {
            double total = 0;
            int x = 0;
            foreach (float f in points)
            {
                double dy = f - (m * x + b);
                total += dy * dy;
                x++;
            }

            return total;
        }

        /// <summary>
        /// Convert an amplitude to decibels.
        /// </summary>
        /// <param name="f">
        /// The amplitude to be converted.
        /// </param>
        /// <returns>
        /// The decibels.
        /// </returns>
        public static float AmplitudeToDecibel(float f)
        {
            return 2 * LinearToDecibel(f);
        }

        /// <summary>
        /// Convert linear to decibel
        /// </summary>
        /// <param name="f">
        /// The input.
        /// </param>
        /// <returns>
        /// The decibel output.
        /// </returns>
        public static float LinearToDecibel(float f)
        {
            return f < 1e-9 ? -90f : (float)(10 * Math.Log10(f));
        }

        /// <summary>
        /// Transpose a 2D matrix.
        /// This means that a 3x5 matrix becomes a 5x3 matrix.
        /// </summary>
        /// <param name="input">
        /// The input matrix.
        /// </param>
        /// <returns>
        /// The transposed matrix.
        /// </returns>
        public static List<float[]> Transpose(List<float[]> input)
        {
            int cols = input.Count;
            int rows = input[0].Length;

            List<float[]> output = new List<float[]>();
            for (int row = 0; row < rows; row++)
            {
                float[] newRow = new float[cols];
                for (int col = 0; col < cols; col++)
                {
                    newRow[col] = input[col][row];
                }

                output.Add(newRow);
            }

            return output;
        }

        //public static Function MeanAbsoluteError(Variable prediction, Variable targets)
        //{
        //    var absolute = CNTKLib.Abs(CNTKLib.Minus(targets, prediction));

        //    return CNTKLib.ReduceMean(absolute, new Axis(-1));
        //}

        //public static Function MeanAbsolutePercentageError(Variable prediction, Variable targets)
        //{
        //    var absolute = CNTKLib.Abs(CNTKLib.Minus(targets, prediction));
        //    var absolutePercentage = CNTKLib.ElementDivide(absolute, targets);
        //    return CNTKLib.ReduceMean(absolutePercentage, new Axis(-1));
        //}

        //public static Function CustomError(Variable prediction, Variable targets, string name = null)
        //{
        //    // predicting a 0 where a 1 should be is VERY bad
        //    var maxPrediction = CNTKLib.Pooling(prediction, PoolingType.Max, new int[] { 3 }, new int[] { 1 });

        //    // predicting a 1 where a 0 should be is bad
        //    var maxTargets = CNTKLib.Pooling(targets, PoolingType.Max, new int[] { 3 }, new int[] { 1 });

        //    return CNTKLib.ReduceSum(CNTKLib.Abs(CNTKLib.Minus(maxPrediction, maxTargets)), new Axis(-1), name);
        //}

        /// <summary>
        /// Find the next power of 2 that is >= n
        /// </summary>
        /// <param name="n">
        /// The input
        /// </param>
        /// <returns>
        /// the next power of 2 that is >= n
        /// </returns>
        public static int NextPowerTwo(int n)
        {
            n--;
            n |= (n >> 1);
            n |= (n >> 2);
            n |= (n >> 4);
            n |= (n >> 8);
            n |= (n >> 16);
            return n++;
        }

        /// <summary>
        /// Find the next power of 2 that is >= n
        /// </summary>
        /// <param name="n">
        /// The input
        /// </param>
        /// <returns>
        /// the next power of 2 that is >= n
        /// </returns>
        public static long NextPowerTwo(long n)
        {
            n--;
            n |= (n >> 1);
            n |= (n >> 2);
            n |= (n >> 4);
            n |= (n >> 8);
            n |= (n >> 16);
            n |= (n >> 32);
            return n++;
        }

        /// <summary>
        /// This mod operator does not match exactly with c++ math.h implementation.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float FMod(float a, float b)
        {
            float q = (float)Math.Floor(a / b);
            return a - q * b;
        }

        /// <summary>
        /// This mod operator does not match exactly with c++ math.h implementation.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double FMod(double a, double b)
        {
            double q = Math.Floor(a / b);
            return a - q * b;
        }

        /// <summary>
        /// Returns the L2 Norm of an array
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static float L2Norm(params float[] array)
        {
            float sum = 0;
            for(int i = 0; i < array.Length; i++)
            {
                sum += array[i] * array[i];
            }

            return (float)Math.Sqrt(sum);
        }

        /// <summary>
        /// Normalize an array so that its max element is 1.
        /// If the largest value is 0 the vector isn't touched
        /// </summary>
        /// <param name="array">
        /// The array to be normalized.
        /// </param>
        public static void Normalize(ref float[] array)
        {
            float max = array.Max();
            if (max != 0)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] /= max;
                }
            }
        }

        /// <summary>
        /// Normalize an array so that its max element is 1.
        /// If the largest value is 0 the vector isn't touched
        /// </summary>
        /// <param name="array">
        /// The array to be normalized.
        /// </param>
        /// <returns>
        /// The normalized copy of the input array.
        /// </returns>
        public static float[] NormalizeClone(float[] array)
        {
            float max = array.Max();
            float[] result = (float[])array.Clone();
            if (max != 0)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] /= max;
                }
            }

            return result;
        }

        /// <summary>
        /// Normalize an array so it's sum is equal to 1. The vector is not touched if it contains negative elements or the sum is zero.
        /// </summary>
        /// <param name="array">
        /// The array to be normalized.
        /// </param>
        public static void NormalizeSum(ref float[] array)
        {
            double sumElements = 0;
            for(int i = 0; i < array.Length; i++)
            {
                if (array[i] < 0)
                {
                    return;
                }

                sumElements += array[i];
            }

            if(sumElements != 0)
            {
                for( int i = 0; i < array.Length; i++)
                {
                    array[i] /= (float)sumElements;
                }
            }
        }

        /// <summary>
        /// Normalize an array so it's sum is equal to 1. The vector is not touched if it contains negative elements or the sum is zero.
        /// </summary>
        /// <param name="array">
        /// The array to be normalized.
        /// </param>
        public static void NormalizeSum(ref List<float> array)
        {
            double sumElements = 0;
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] < 0)
                {
                    return;
                }

                sumElements += array[i];
            }

            if (sumElements != 0)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    array[i] /= (float)sumElements;
                }
            }
        }

        /// <summary>
        /// Return the index of the max item in the list.
        /// </summary>
        /// <typeparam name="T">
        /// The type of object contained in the list.
        /// </typeparam>
        /// <param name="items">
        /// The input list to check.
        /// </param>
        /// <returns>
        /// The index of the largest element.
        /// </returns>
        public static int ArgMax<T>(IEnumerable<T> items) where T : IComparable
        {
            int index = 0;
            T max = default(T);
            int iMax = 0;
            foreach(T item in items)
            {
                if (index == 0 || item.CompareTo(max) > 0)
                {
                    max = item;
                    iMax = index;
                }

                index++;
            }

            return iMax;
        }

        /// <summary>
        /// Return the index of the max item in the array.
        /// </summary>
        /// <param name="items">
        /// The input array to check.
        /// </param>
        /// <returns>
        /// The index of the largest element.
        /// </returns>
        public static int ArgMax(float[] items)
        {
            if (items.Length <= 1)
            {
                return 0;
            }

            float max = items[0];
            int iMax = 0;
            for (int i = 1; i < items.Length; i++)
            {
                if (items[i] > max)
                {
                    max = items[i];
                    iMax = i;
                }
            }

            return iMax;
        }

        /// <summary>
        /// Return the index of the max item in the list.
        /// </summary>
        /// <param name="items">
        /// The input list to check.
        /// </param>
        /// <returns>
        /// The index of the largest element.
        /// </returns>
        public static int ArgMax(List<float> items)
        {
            if (items.Count <= 1)
            {
                return 0;
            }

            float max = items[0];
            int iMax = 0;
            for (int i = 1; i < items.Count; i++)
            {
                if (items[i] > max)
                {
                    max = items[i];
                    iMax = i;
                }
            }

            return iMax;
        }

        /// <summary>
        /// Return the index of the min item in the list.
        /// </summary>
        /// <typeparam name="T">
        /// The type of object contained in the list.
        /// </typeparam>
        /// <param name="items">
        /// The input list to check.
        /// </param>
        /// <returns>
        /// The index of the largest element.
        /// </returns>
        public static int ArgMin<T>(IEnumerable<T> items) where T : IComparable
        {
            int index = 0;
            T min = default(T);
            int iMin = 0;
            foreach (T item in items)
            {
                if (index == 0 || item.CompareTo(min) < 0)
                {
                    min = item;
                    iMin = index;
                }

                index++;
            }

            return iMin;
        }

        /// <summary>
        /// Compute the average of a list of floats.
        /// Not sure why i implemented this instead of using LINQ.Average()
        /// </summary>
        /// <param name="collection">
        /// </param>
        /// <returns></returns>
        public static float Mean(IEnumerable<float> collection)
        {
            int count = 0;
            double sum = 0;
            foreach(float f in collection)
            {
                sum += f;
                count++;
            }

            return (float)(sum / Math.Max(count, 1));
        }

        /// <summary>
        /// compute the standard deviation of a list.
        /// </summary>
        /// <param name="collection">
        /// The list.
        /// </param>
        /// <param name="mean">
        /// The average of the list.
        /// </param>
        /// <returns>
        /// The standard deviation of the list.
        /// </returns>
        public static float StdDev(IEnumerable<float> collection, float mean)
        {
            int N = 0;
            double stdev = 0;
            foreach(float f in collection)
            {
                stdev += Math.Pow(f - mean, 2);
                N++;
            }

            return (float)Math.Sqrt(stdev / Math.Max(N, 1));
        }

        /// <summary>
        /// Create a histogram with bin width = 1.
        /// </summary>
        /// <param name="input">
        /// The input list to create a histogram from.
        /// </param>
        /// <returns>
        /// A histogram.
        /// </returns>
        public static List<float> BinCount(List<float> input)
        {
            List<float> output = new List<float>();
            int size = (int)(Math.Max(input.Max(), 0) + 0.5f) + 1;
            for(int i = 0; i < size; i++)
            {
                output.Add(0);
            }

            int index = 0;
            for(int i = 0; i < input.Count; i++)
            {
                index = (int)(Math.Max(input[i], 0) + 0.5);
                if (index < output.Count)
                {
                    output[index]++;
                }
            }

            return output;
        }

        /// <summary>
        /// Compute log base 2 of a number.
        /// </summary>
        /// <param name="n">
        /// The input number.
        /// </param>
        /// <returns>
        /// The log base 2 of n.
        /// </returns>
        public static float Log2(double n)
        {
            return (float)(Math.Log(n) / Math.Log(2));
        }

        /// <summary>
        /// Add up the elements of a list.
        /// </summary>
        /// <param name="list">
        /// The input list.
        /// </param>
        /// <param name="inclusiveStartIndex">
        /// The index to start summing at.
        /// </param>
        /// <param name="exclusiveEndIndex">
        /// The index to stop summing at.
        /// End index is exclusive so you can do Accumulate(array, n, array.Length)
        /// </param>
        /// <returns>
        /// The sum from list[inclusiveStartIndex, exclusiveEndIndex)
        /// </returns>
        public static float Accumulate(List<float> list, int inclusiveStartIndex, int exclusiveEndIndex)
        {
            float sum = 0;
            for (int i = inclusiveStartIndex; i < list.Count && i < exclusiveEndIndex; i++)
            {
                sum += list[i];
            }

            return sum;
        }

        /// <summary>
        /// End index is exclusive so you can do Accumulate(array, n, array.Length)
        /// </summary>
        /// <param name="list">
        /// The input list.
        /// </param>
        /// <param name="inclusiveStartIndex">
        /// The index to start summing at.
        /// </param>
        /// <param name="exclusiveEndIndex">
        /// The index to stop summing at.
        /// </param>
        /// <returns>
        /// The sum from list[inclusiveStartIndex, exclusiveEndIndex)
        /// </returns>
        public static float Accumulate(float[] list, int inclusiveStartIndex, int exclusiveEndIndex)
        {
            float sum = 0;
            for(int i = inclusiveStartIndex; i < list.Length && i < exclusiveEndIndex; i++)
            {
                sum += list[i];
            }

            return sum;
        }
    }
}
