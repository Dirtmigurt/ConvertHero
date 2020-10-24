namespace ConvertHero.AudioFileHelpers
{
    using MathNet.Numerics;

    /// <summary>
    /// Class for converting comlex numbers to Polar notation (a + bi) => r(theta)
    /// </summary>
    public static class CartesianToPolar
    {
        /// <summary>
        /// Convert a list of complex numbers into their component magnitudes and phases.
        /// </summary>
        /// <param name="complexSignal">
        /// The input list of complex numbers.
        /// </param>
        /// <returns>
        /// The magnitude and phase angle of each of the input numbers.
        /// </returns>
        public static (float[] Magnitude, float[] Phase) ConvertComplexToPolar(Complex32[] complexSignal)
        {

            float[] Magnitude = new float[complexSignal.Length];
            float[] Phase = new float[complexSignal.Length];

            for(int i = 0; i < complexSignal.Length; i++)
            {
                Magnitude[i] = complexSignal[i].Magnitude;
                Phase[i] = complexSignal[i].Phase;
            }

            return (Magnitude, Phase);
        }
    }
}
