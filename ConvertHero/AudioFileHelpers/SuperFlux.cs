using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class SuperFlux
    {
        int binWidth = 0;
        bool causal = false;
        int frameWidth = 0;
        List<float[]> bands = new List<float[]>();
        MaxFilter maxFilter;

        public SuperFlux(int binWidth, int frameWidth, bool causal = false)
        {
            this.binWidth = binWidth;
            this.frameWidth = frameWidth;
            this.causal = causal;
            this.maxFilter = new MaxFilter(this.binWidth, this.causal);
        }

        public float Compute(float[] newBand)
        {
            this.bands.Add(newBand);
            while(this.bands.Count > this.frameWidth + 1 && this.bands.Count > 0)
            {
                this.bands.RemoveAt(0);
            }

            if (this.bands.Count < this.frameWidth + 1)
            {
                return 0;
            }

            int nFrames = this.bands.Count;
            int nBands = this.bands[0].Length;
            
            float[] maxsBuffer = new float[nBands];

            // buffer for differences
            float diffs = 0;
            maxsBuffer = this.maxFilter.Filter(this.bands[0]);

            for(int j = 0; j < nBands; j++)
            {
                float cur_diff = this.bands[this.frameWidth][j] - maxsBuffer[j];
                if(cur_diff > 0.0f)
                {
                    diffs += cur_diff;
                }
            }

            return diffs;
        }
    }
}
