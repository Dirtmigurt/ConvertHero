﻿namespace ConvertHero.AudioFileHelpers
{
    using NAudio.Wave;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class SampleReader : IDisposable
    {
        private WaveStream Reader;
        private ISampleProvider SampleProvider;
        private List<float> samples = new List<float>();
        private long approxTotalSamples = 0;
        private int windowSizeSamples = -1;
        private double windowSizeSeconds = -1;
        private int stepSize = -1;
        public int SampleRate = -1;

        /// <summary>
        /// Creates a new audio file frame reader. The reader will read the file frame by frame where the frame contains
        /// n samples where n == windowSizeSamples and the center point of each frame advances by 1 / frameRate seconds.
        /// </summary>
        /// <param name="fileName">
        /// The audio file that contains lots of samples!
        /// </param>
        /// <param name="frameRate">
        /// How many frames per second should be read. The time offset of the frames will advance by 1/frameRate on each read.
        /// </param>
        /// <param name="windowSizeSamples">
        /// The number of seconds that each frame should span, this allows the window to remain the same when multiple files with
        /// different SampleRates are used. windowSampleSize = windowSizeSeconds * SampleRate
        /// </param>
        public SampleReader(string fileName, int frameRate, double windowSizeSeconds)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                throw new ArgumentException($"file:\"{fileName}\" does not exist");
            }

            this.Initialize(fileName, frameRate);
            this.windowSizeSamples = (int)(this.SampleRate * windowSizeSeconds);
            this.windowSizeSeconds = windowSizeSamples / (double)this.SampleRate;
        }

        /// <summary>
        /// Creates a new audio file frame reader. The reader will read the file frame by frame where the frame contains
        /// n samples where n == windowSizeSamples and the center point of each frame advances by 1 / frameRate seconds.
        /// </summary>
        /// <param name="fileName">
        /// The audio file that contains lots of samples!
        /// </param>
        /// <param name="frameRate">
        /// How many frames per second should be read. The time offset of the frames will advance by 1/frameRate on each read.
        /// </param>
        /// <param name="windowSizeSamples">
        /// The number of samples in each frame. The amound of time contained in the frame is windowSizeSamples/SampleRate.
        /// This means that two files with different SampleRate will represent different amounds of time with 1024 samples.
        /// </param>
        public SampleReader(string fileName, int frameRate, int windowSizeSamples)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                throw new ArgumentException($"file:\"{fileName}\" does not exist");
            }

            this.Initialize(fileName, frameRate);
            this.windowSizeSamples = windowSizeSamples;
            this.windowSizeSeconds = windowSizeSamples / (double)this.SampleRate;
        }

        /// <summary>
        /// Initialize the sample reader with a file, and framerate.
        /// </summary>
        /// <param name="fileName">
        /// The name of the audio file that should be read.
        /// </param>
        /// <param name="frameRate">
        /// The frame rate to read samples in at.
        /// </param>
        private void Initialize(string fileName, int frameRate)
        {
            WaveStream reader = null;
            ISampleProvider provider;
            if (fileName.EndsWith(".ogg"))
            {
                reader = new NAudio.Vorbis.VorbisWaveReader(fileName);
                NAudio.Vorbis.VorbisWaveReader vReader = new NAudio.Vorbis.VorbisWaveReader(fileName);
                provider = vReader.ToMono();
            }
            else
            {
                reader = new AudioFileReader(fileName);
                AudioFileReader afReader = new AudioFileReader(fileName);
                provider = afReader.ToMono();
            }

            this.SampleProvider = provider;
            this.Reader = reader;
            this.SampleRate = reader.WaveFormat.SampleRate;
            this.stepSize = this.SampleRate / frameRate;
            this.approxTotalSamples = (long)reader.TotalTime.TotalSeconds * this.SampleRate;
        }

        /// <summary>
        /// Gets the next frame of the audio file.
        /// If the frame rate is 50, this would need to be called 50 times to advance 1 second in the signal.
        /// </summary>
        /// <param name="buffer">
        /// Array that contains the samples in the window/frame.
        /// </param>
        /// <returns>
        /// Number of samples read from the file and put into the buffer.
        /// </returns>
        public int Read(out float[] buffer)
        {
            buffer = new float[this.windowSizeSamples];
            while (samples.Count < this.windowSizeSamples)
            {
                float[] b = new float[this.SampleRate];
                int count = this.SampleProvider.Read(b, 0, b.Length);
                for (int i = 0; i < count; i++)
                {
                    samples.Add(b[i]);
                }

                if (count == 0)
                {
                    break;
                }
            }

            int n = Math.Min(this.windowSizeSamples, this.samples.Count);
            for (int i = 0; i < n; i++)
            {
                buffer[i] = this.samples[i];
            }

            for (int i = 0; i < stepSize && this.samples.Count > 0; i++)
            {
                this.samples.RemoveAt(0);
            }

            return n;
        }

        /// <summary>
        /// Read the entire signal into a single array.
        /// </summary>
        /// <param name="maxSamples">
        /// Allows truncating the samples for testing like readinly only the first 1,000,000 samples.
        /// </param>
        /// <returns>
        /// An array contain all samples in the audio file.
        /// </returns>
        public float[] ReadAll(int maxSamples = int.MaxValue)
        {
            List<float> samples = new List<float>();
            float[] buffer = new float[(1 << 14)];
            while(true)
            {
                int count = this.SampleProvider.Read(buffer, 0, buffer.Length);
                for(int i = 0; i < count; i++)
                {
                    samples.Add(buffer[i]);
                }

                if (count == 0 || samples.Count >= maxSamples)
                {
                    break;
                }
            }

            return samples.ToArray();
        }

        /// <summary>
        /// Guess the number of frames that will be returned by the sample reader.
        /// </summary>
        /// <param name="frameRate">
        /// Any custom frame rate.
        /// </param>
        /// <returns>
        /// The number of frames it would take to get through the whole signal.
        /// </returns>
        public long TotalFrames(int frameRate)
        {
            return (long)(this.Reader.TotalTime.TotalSeconds * frameRate);
        }

        /// <summary>
        /// Release any system resources.
        /// </summary>
        public void Dispose()
        {
            if (this.Reader != null)
            {
                this.Reader.Dispose();
                this.Reader = null;
            }
        }
    }
}
