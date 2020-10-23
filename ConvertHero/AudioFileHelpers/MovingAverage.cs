using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class MovingAverage
    {
        private IIR filter;
        float[] b;
        float[] a;

        public MovingAverage(int size)
        {
            this.b = new float[size];
            for(int i = 0; i < size; i++)
            {
                this.b[i] = 1.0f / size;
            }

            this.a = new float[] { 1.0f };

            this.filter = new IIR(b, a);
        }

        public float[] Compute(float[] input)
        {
            this.filter.Reset();
            return this.filter.Compute(input);
        }
    }
}
