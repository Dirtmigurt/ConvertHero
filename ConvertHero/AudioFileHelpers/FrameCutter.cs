namespace ConvertHero.AudioFileHelpers
{
    using System;

    public class FrameCutter
    {
        /// <summary>
        /// Buffer contains the ENTIRE signal. Pieces of this array are returned as frames.
        /// </summary>
        private float[] buffer;

        /// <summary>
        /// Whether to start the first frame at time 0 (centered at framesize/2) if true, or -framesize/2 otherwise (zero-centered)
        /// </summary>
        private bool startFromZero = false;

        /// <summary>
        /// Whether the beginning of the last frame should reach the end of file. only applicable if startFromZero is true.
        /// </summary>
        private bool lastFrameToEndOfFile = false;

        /// <summary>
        /// The current index into the buffer[].
        /// </summary>
        private int startIndex = 0;

        /// <summary>
        /// The size of the frame returned on each call to GetNextFrame()
        /// </summary>
        private int frameSize = 1024;

        /// <summary>
        /// How many samples should be advanced between frames.
        /// </summary>
        private int hopSize = 512;

        /// <summary>
        /// What percentage of the frameSize should still be treated as a frame by padding zeros.
        /// </summary>
        private int validFrameThreshold;

        /// <summary>
        /// Has this reader reached the end of the buffer?
        /// </summary>
        private bool lastFrame = false;

        /// <summary>
        /// Class the divides some buffer up into frames and returns them one at a time.
        /// </summary>
        /// <param name="buffer">
        /// The source signal to be cut into frames.
        /// </param>
        /// <param name="frameSize">
        /// The number of samples or items from the buffer that make up a single frame.
        /// </param>
        /// <param name="hopSize">
        /// how far to move in the buffer between frames. Hop size >= frameSize means that adjacent frames do not overlap
        /// </param>
        /// <param name="validFrameThresholdRatio">
        /// What percentage of the frameSize should still be treated as a frame by padding zeros.
        /// </param>
        /// <param name="startFromZero">
        /// Whether to start the first frame at time 0 (centered at framesize/2) if true, or -framesize/2 otherwise (zero-centered)
        /// </param>
        /// <param name="lastFrameToEndOfFile">
        /// Whether the beginning of the last frame should reach the end of file. only applicable if startFromZero is true.
        /// </param>
        public FrameCutter(float[] buffer, int frameSize = 1024, int hopSize = 512, float validFrameThresholdRatio = 0, bool startFromZero = false, bool lastFrameToEndOfFile = false)
        {
            this.buffer = buffer;
            this.frameSize = frameSize;
            this.hopSize = hopSize;
            this.startFromZero = startFromZero;
            this.lastFrameToEndOfFile = lastFrameToEndOfFile;
            if (validFrameThresholdRatio > 0.5 && !this.startFromZero)
            {
                throw new Exception($"FrameCutter: validFrameThresholdRatio cannot be larger than 0.5 if startFromZero is false (this is to prevent loss of the first frame which would be only half a valid frame since the first frame is centered on teh beginning of the audio)");
            }

            this.validFrameThreshold = (int)validFrameThresholdRatio * this.frameSize;
            if (this.startFromZero)
            {
                this.startIndex = 0;
            }
            else
            {
                this.startIndex = -(this.frameSize + 1) / 2;
            }
        }

        /// <summary>
        /// Class the divides some buffer up into frames and returns them one at a time.
        /// </summary>
        /// <param name="frameSize">
        /// The number of samples or items from the buffer that make up a single frame.
        /// </param>
        /// <param name="hopSize">
        /// how far to move in the buffer between frames. Hop size >= frameSize means that adjacent frames do not overlap
        /// </param>
        /// <param name="validFrameThresholdRatio">
        /// What percentage of the frameSize should still be treated as a frame by padding zeros.
        /// </param>
        /// <param name="startFromZero">
        /// Whether to start the first frame at time 0 (centered at framesize/2) if true, or -framesize/2 otherwise (zero-centered)
        /// </param>
        /// <param name="lastFrameToEndOfFile">
        /// Whether the beginning of the last frame should reach the end of file. only applicable if startFromZero is true.
        /// </param>
        public FrameCutter(int frameSize = 1024, int hopSize = 512, float validFrameThresholdRatio = 0, bool startFromZero = false, bool lastFrameToEndOfFile = false)
        {
            this.frameSize = frameSize;
            this.hopSize = hopSize;
            this.startFromZero = startFromZero;
            this.lastFrameToEndOfFile = lastFrameToEndOfFile;

            if (validFrameThresholdRatio > 0.5 && !this.startFromZero)
            {
                throw new Exception($"FrameCutter: validFrameThresholdRatio cannot be larger than 0.5 if startFromZero is false (this is to prevent loss of the first frame which would be only half a valid frame since the first frame is centered on teh beginning of the audio)");
            }

            this.validFrameThreshold = (int)validFrameThresholdRatio * this.frameSize;
            if (this.startFromZero)
            {
                this.startIndex = 0;
            }
            else
            {
                this.startIndex = -(this.frameSize + 1) / 2;
            }
        }

        /// <summary>
        /// Allows the initialization of a framecutter before the entire signal is available.
        /// </summary>
        /// <param name="buffer"></param>
        public void SetBuffer(float[] buffer)
        {
            this.buffer = buffer;
            this.startIndex = 0;
            this.lastFrame = false;
        }

        /// <summary>
        /// Return the next chunk of the buffer[].
        /// </summary>
        /// <returns>
        /// Returns the next chunk of the buffer[].
        /// </returns>
        public float[] GetNextFrame()
        {
            if (this.lastFrame || this.buffer == null || this.buffer.Length == 0)
            {
                return null;
            }

            if (this.startIndex >= this.buffer.Length)
            {
                return null;
            }

            float[] frame = new float[this.frameSize];
            int frameIndex = 0;

            // This would only happen on the first frame if startFromZero == false
            if (this.startIndex < 0)
            {
                int zeroFill = Math.Min(-this.startIndex, this.frameSize);
                for(; frameIndex < zeroFill; frameIndex++)
                {
                    frame[frameIndex] = 0;
                }
            }

            int fill = Math.Min(this.frameSize, this.buffer.Length - this.startIndex) - frameIndex;
            for(int i = 0; i < fill; i++)
            {
                frame[frameIndex + i] = buffer[this.startIndex + frameIndex + i];
            }

            frameIndex += fill;

            // check if the frameIndex is below the threshold (this would only happen for the last frame in the stream)
            if (frameIndex < this.validFrameThreshold)
            {
                this.lastFrame = true;
                return null;
            }

            if (this.startIndex + frameIndex >= this.buffer.Length &&
                this.startFromZero && !this.lastFrameToEndOfFile)
            {
                this.lastFrame = true;
            }

            if(frameIndex < this.frameSize)
            {
                if (this.startFromZero)
                {
                    if (this.lastFrameToEndOfFile)
                    {
                        if (this.startIndex >= this.buffer.Length)
                        {
                            this.lastFrame = true;
                        }
                    }
                    else
                    {
                        this.lastFrame = true;
                    }
                }
                else
                {
                    if (this.startIndex + this.frameSize / 2 >= this.buffer.Length)
                    {
                        this.lastFrame = true;
                    }
                }

                // fill in the frame with 0 to the end
                for(; frameIndex < this.frameSize; frameIndex++)
                {
                    frame[frameIndex] = 0;
                }
            }

            this.startIndex += this.hopSize;
            return frame;
        }
    }
}
