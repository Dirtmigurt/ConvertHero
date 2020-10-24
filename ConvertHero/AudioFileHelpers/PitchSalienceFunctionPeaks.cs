namespace ConvertHero.AudioFileHelpers
{
    using System;

    public class PitchSalienceFunctionPeaks
    {
        /// <summary>
        /// A Peak detector
        /// </summary>
        PeakDetection peakDetector;

        /// <summary>
        /// Initializes a new instance of the PitchSalienceFunctionPeaks.
        /// It simply detects peaks in a pitch salience function;
        /// </summary>
        /// <param name="binResolution">salience function bin resolution [cents]</param>
        /// <param name="minFrequency">the minimum frequency to evaluate (ignore peaks below) [Hz]</param>
        /// <param name="maxFrequency">the maximum frequency to evaluate (ignore peaks above) [Hz]</param>
        /// <param name="referenceFrequency">the reference frequency for Hertz to cent convertion [Hz], corresponding to the 0th cent bin</param>
        public PitchSalienceFunctionPeaks(float binResolution = 10, float minFrequency = 55, float maxFrequency = 1760, float referenceFrequency = 55)
        {
            float numberBins = (float)Math.Floor(6000.0 / binResolution) - 1;
            float binsInOctave = 1200 / binResolution;
            float minBin = (float)Math.Max(0, Math.Floor(binsInOctave * MathHelpers.Log2(minFrequency / referenceFrequency) + 0.5));
            float maxBin = (float)Math.Max(0, Math.Floor(binsInOctave * MathHelpers.Log2(maxFrequency / referenceFrequency) + 0.5));
            maxBin = Math.Min(numberBins, maxBin);

            this.peakDetector = new PeakDetection(minBin, maxBin, interpolate: false, range: numberBins, maxPeaks: 100, orderby: OrderByType.Amplitude);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="salienceFunction">the array of salience function values corresponding to cent frequency bins</param>
        /// <returns>
        /// salienceBins = the cent bins corresponding to salience function peaks
        /// salienceValues = the values of salience function peaks
        /// </returns>
        public (float[] salienceBins, float[] salienceValues) Compute(float[] salienceFunction)
        {
            return this.peakDetector.Compute(salienceFunction);
        }
    }
}
