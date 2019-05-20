using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public class MultiFileSampleReader : IDisposable
    {
        public int windowSizeSamples;
        public int SampleRate = -1;
        private List<SampleReader> readers = new List<SampleReader>();

        public MultiFileSampleReader(List<string> audioFiles, int frameRate, int samplesPerWindow)
        {
            windowSizeSamples = samplesPerWindow;
            foreach(string audioFile in audioFiles)
            {
                SampleReader reader = new SampleReader(audioFile, frameRate, samplesPerWindow);
                readers.Add(reader);

                if(this.SampleRate > 0 && reader.SampleRate != this.SampleRate)
                {
                    throw new Exception($"{audioFile} has a sample rate of {reader.SampleRate:N0} Hz while other files in the same directory have a sample rate of {this.SampleRate:N0} Hz");
                }

                this.SampleRate = reader.SampleRate;
            }
        }

        public void Dispose()
        {
            foreach(SampleReader reader in readers)
            {
                if(reader != null)
                {
                    reader.Dispose();
                }
            }

            readers.Clear();
        }

        public int Read(out float[] buffer)
        {
            buffer = new float[this.windowSizeSamples];
            int count = -1;
            foreach(SampleReader reader in this.readers)
            {
                float[] tempbuffer = new float[this.windowSizeSamples];
                int tempcount = reader.Read(out tempbuffer);

                if (tempcount > count)
                {
                    count = tempcount;
                }

                for(int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] += tempbuffer[i];
                }
            }

            return count;
        }
    }
}
