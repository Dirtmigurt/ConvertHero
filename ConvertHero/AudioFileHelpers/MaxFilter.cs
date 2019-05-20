using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class MaxFilter
    {
        private int width = 0;

        private int halfWidth = 0;

        private bool causal = false;

        private int bufferFillIndex = 0;

        private bool filledBuffer = false;

        private float currentMax = 0;

        private float[] buffer;

        public MaxFilter(int width, bool causal = false)
        {
            this.width = width;
            this.causal = causal;
            if (width % 2 == 0)
            {
                width++;
            }

            this.halfWidth = (width - 1) / 2;
            this.bufferFillIndex = causal ? 0 : this.halfWidth;
        }

        public float[] Filter(float[] input)
        {
            int size = input.Length;
            float[] filtered = new float[size];

            int readIndex = 0;

            if(!this.filledBuffer)
            {
                if (this.bufferFillIndex == (this.causal ? 0 : this.halfWidth))
                {
                    this.currentMax = input[0];

                    // create buffer filled with this.currentMax
                    this.buffer = new float[this.width];
                    for(int i = 0; i < this.width; i++)
                    {
                        this.buffer[i] = this.currentMax;
                    }
                }

                int maxIndex = Math.Min(size, width - this.bufferFillIndex);
                for(int i = 0; i < maxIndex; i++)
                {
                    this.buffer[this.bufferFillIndex] = input[readIndex];
                    this.currentMax = Math.Max(input[readIndex], this.currentMax);
                    filtered[i] = this.currentMax;
                    readIndex++;
                    this.bufferFillIndex++;
                }

                this.filledBuffer = this.bufferFillIndex == this.width;
            }

            // Fill and compute max of the current circular buffer
            for(int j = readIndex; j < size; j++)
            {
                this.bufferFillIndex %= this.width;
                this.buffer[this.bufferFillIndex] = input[j];
                filtered[j] = this.buffer.Max();
                this.bufferFillIndex++;
            }

            return filtered;
        }

        public void Reset()
        {
            this.buffer = null;
            this.filledBuffer = false;
            this.bufferFillIndex = 0;
        }
    }
}
