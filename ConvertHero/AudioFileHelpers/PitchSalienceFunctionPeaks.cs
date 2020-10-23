using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class PitchSalienceFunctionPeaks
    {
        PeakDetection peakDetector;

        public PitchSalienceFunctionPeaks(float binResolution = 10, float minFrequency = 55, float maxFrequency = 1760, float referenceFrequency = 55)
        {
            float numberBins = (float)Math.Floor(6000.0 / binResolution) - 1;
            float binsInOctave = 1200 / binResolution;
            float minBin = (float)Math.Max(0, Math.Floor(binsInOctave * MathHelpers.Log2(minFrequency / referenceFrequency) + 0.5));
            float maxBin = (float)Math.Max(0, Math.Floor(binsInOctave * MathHelpers.Log2(maxFrequency / referenceFrequency) + 0.5));
            maxBin = Math.Min(numberBins, maxBin);

            this.peakDetector = new PeakDetection(minBin, maxBin, interpolate: false, range: numberBins, maxPeaks: 100, orderby: OrderByType.Amplitude);
        }

        public (float[] salienceBins, float[] salienceValues) Compute(float[] salienceFunction)
        {
            return this.peakDetector.Compute(salienceFunction);
        }
    }
}
