namespace ConvertHero.AudioFileHelpers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// This class allows reading of multiple audio files and merging their samples into a single stream.
    /// </summary>
    public class MultiFileSampleReader : IDisposable
    {
        /// <summary>
        /// The size of a window into the signal.
        /// </summary>
        public int windowSizeSamples;

        /// <summary>
        /// the sample rate of the audio files.
        /// </summary>
        public int SampleRate = -1;

        /// <summary>
        /// The readers responsible for reading each individual file.
        /// </summary>
        private List<SampleReader> readers = new List<SampleReader>();

        /// <summary>
        /// Create a new instance of the MultFileSampleReader
        /// </summary>
        /// <param name="audioFiles">
        /// The List of audio files to read.
        /// </param>
        /// <param name="frameRate">
        /// How many frames/sec should the sampling be done at.
        /// At a sample rate of 44100, a frame rate of 100 means that each window advances 44100/100 => 441 samples
        /// </param>
        /// <param name="samplesPerWindow">
        /// How wide should the window into the signal be.
        /// </param>
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

        /// <summary>
        /// Clean up all resources consumed by the SampleReader instances.
        /// </summary>
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

        /// <summary>
        /// Get the next window from each of the sample reader instances.
        /// </summary>
        /// <param name="buffer">
        /// the array to put the samples into.
        /// </param>
        /// <returns>
        /// The number of samples read.
        /// </returns>
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
