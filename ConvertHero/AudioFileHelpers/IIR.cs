using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class IIR
    {
        private float[] a;
        private float[] b;
        private float[] state;

        public IIR(float[] numerators, float[] denominators)
        {
            this.a = denominators;
            this.b = numerators;

            for(int i = 1; i < a.Length; i++)
            {
                a[i] /= a[0];
            }

            for(int i = 0; i < b.Length; i++)
            {
                b[i] /= a[0];
            }

            a[0] = 1.0f;

            int wantedSize = (int)Math.Max(b.Length, a.Length);
            this.state = new float[wantedSize];
        }

        private void UpdateStateLine(int size, float x, float y)
        {
            for(int k = 1; k < size; k++)
            {
                this.state[k - 1] = (this.b[k] * x - this.a[k] * y) + this.state[k];
                Renormalize(ref this.state[k - 1]);
            }
        }

        public float[] Compute(float[] x)
        {
            float[] output = new float[x.Length];

            if(this.b.Length == this.a.Length)
            {
                for(int n = 0; n < output.Length; n++)
                {
                    output[n] = this.b[0] * x[n] + this.state[0];
                    this.UpdateStateLine(this.state.Length, x[n], output[n]);
                }
            }
            else if(this.b.Length > this.a.Length)
            {
                for (int n = 0; n < output.Length; n++)
                {
                    output[n] = this.b[0] * x[n] + this.state[0];
                    this.UpdateStateLine(this.a.Length, x[n], output[n]);

                    for (int k = a.Length; k < this.state.Length; k++)
                    {
                        this.state[k - 1] = this.b[k] * x[n] + this.state[k];
                        Renormalize(ref this.state[k - 1]);
                    }
                }
            }
            else
            {
                for (int n = 0; n < output.Length; n++)
                {
                    output[n] = this.b[0] * x[n] + this.state[0];
                    this.UpdateStateLine(this.b.Length, x[n], output[n]);

                    for (int k = b.Length; k < this.state.Length; k++)
                    {
                        this.state[k - 1] = (-this.a[k] * output[n]) + this.state[k];
                        Renormalize(ref this.state[k - 1]);
                    }
                }
            }

            return output;
        }

        public void Reset()
        {
            this.state = new float[this.state.Length];
        }

        /// <summary>
        /// x86_64 specific implementation
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        private static bool IsDenormal(float f)
        {
            // when 0, the exponent will also be 0 and will break
            // the rest of this algorithm, so we should check for
            // this first
            if (f == 0f)
            {
                return false;
            }

            // Get the bits
            byte[] buffer = BitConverter.GetBytes(f);
            int bits = BitConverter.ToInt32(buffer, 0);
            // extract the exponent, 8 bits in the upper registers,
            // above the 23 bit significand
            int exponent = (bits >> 23) & 0xff;
            // check and see if anything is there!
            return exponent == 0;
        }

        private static void Renormalize(ref float f)
        {
            if(IsDenormal(f))
            {
                f = 0.0f;
            }
        }
    }
}
