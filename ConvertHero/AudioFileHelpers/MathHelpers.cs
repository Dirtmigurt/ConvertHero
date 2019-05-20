using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public static class MathHelpers
    {
        private const float SilenceCutoff = 1e-9f;
        private const float SilenceCutoffDecibel = -90;

        public static float HertzToMel(float hz)
        {
            return (float)(1127.01048 * Math.Log(hz / 700.0 + 1.0));
        }

        public static float MelToHertz(float mel)
        {
            return (float)(700.0 * (Math.Exp(mel / 1127.01048) - 1.0));
        }

        public static float HertzToMel10(float hz)
        {
            return (float)(2595.0 * Math.Log10(hz / 700.0 + 1.0));
        }

        public static float Mel10ToHertz(float mel)
        {
            return (float)(700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0));
        }

        // Convert Hz to Mel based on Slaney's formula in MATLAB Auditory Toolbox
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

        public static float HertzToHertz(float hz)
        {
            return hz;
        }

        public static float HertzToCents(float hz)
        {
            return (float)(12 * Math.Log(hz / 440.0) / Math.Log(2.0) + 69.0);
        }

        public static float AmplitudeToDecibel(float f)
        {
            return 2 * LinearToDecibel(f);
        }

        public static float LinearToDecibel(float f)
        {
            return f < 1e-9 ? -90f : (float)(10 * Math.Log10(f));
        }

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
    }
}
