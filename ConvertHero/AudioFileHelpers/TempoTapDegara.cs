namespace ConvertHero.AudioFileHelpers
{
    using Accord.Math;
    using Melanchall.DryWetMidi.Smf;
    using NAudio.Wave;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// This algorithm estimates beat positions given an onset detection function.  
    /// The detection function is partitioned into 6-second frames with a 1.5-second increment, and the autocorrelation is computed for each frame, and is weighted by a tempo preference curve [2]. 
    /// Periodicity estimations are done frame-wisely, searching for the best match with the Viterbi algorith [3]. 
    /// The estimated periods are then passed to the probabilistic beat tracking algorithm [1], which computes beat positions.
    /// 
    ///  References:
    ///   [1] Degara, N., Rua, E. A., Pena, A., Torres-Guijarro, S., Davies, M. E., & Plumbley, M. D. (2012). Reliability-informed beat tracking of musical signals. Audio, Speech, and Language Processing, IEEE Transactions on, 20(1), 290-301. 
    ///   [2] Davies, M. E., & Plumbley, M. D. (2007). Context-dependent beat tracking of musical audio. Audio, Speech, and Language Processing, IEEE Transactions on, 15(3), 1009-1020. 
    ///   [3] Stark, A. M., Davies, M. E., & Plumbley, M. D. (2009, September). Real-time beatsynchronous analysis of musical audio. In 12th International Conference on Digital Audio Effects (DAFx-09), Como, Italy.
    /// </summary>
    public class TempoTapDegara
    {
        #region DaviesBeatPeriod

        /// <summary>
        /// Half of the smoothing window size.
        /// </summary>
        private int smoothingWindowHalfSize;

        /// <summary>
        /// The number of comb filters to use.
        /// </summary>
        private const int numberCombs = 4;

        /// <summary>
        /// duration of one of the OnsetDetectionFunction frames.
        /// </summary>
        private float frameDurationODF;

        /// <summary>
        /// sample rate of the onset detection function.
        /// </summary>
        private float sampleRateODF;

        /// <summary>
        /// Frame size of the onset detection function.
        /// </summary>
        private int frameSizeODF;

        /// <summary>
        /// hop size of the onset detection function.
        /// </summary>
        private int hopSizeODF;

        /// <summary>
        /// duration in time that is jumped on each hop of the ODF.
        /// </summary>
        private float hopDurationODF;

        /// <summary>
        /// How many samples should be interpolated?
        /// </summary>
        private int resample;

        /// <summary>
        /// How many frames are in the onset detection function.
        /// </summary>
        private int numberFramesODF;

        /// <summary>
        /// The minimum period index.
        /// </summary>
        private int periodMinIndex;

        /// <summary>
        /// The maximum period index.
        /// </summary>
        private int periodMaxIndex;

        /// <summary>
        ///  The maximum period user index.
        /// </summary>
        private int periodMaxUserIndex;

        /// <summary>
        /// The minimum period user index.
        /// </summary>
        private int periodMinUserIndex;

        /// <summary>
        ///   Tempo preference weights (Rayleigh distribution) with a peak at 120 BPM,
        ///   equal to pow(43, 2) with the default ODF sample rate (44100./512).
        ///   Maximum period of ODF to consider (period of 512 ODF samples with the
        ///   default settings) correspond to 512 * 512. / 44100. = ~6 secs
        /// </summary>
        float[] tempoWeights;

        /// <summary>
        ///   this  is a this.hopSizeODF x this.hopSizeODF matrix, where each column i consists of a gaussian centered at i, with stddev=8 by default (i.e., when this.hopSizeODF=128), and leave
        ///   columns before 28th and after 108th zeroed, as well as the lines before 28th and after 108th. 
        /// </summary>
        List<List<float>> transitionsViterbi;

        /// <summary>
        /// A Helper object used to compute auto-correlation.
        /// </summary>
        AutoCorrelation autocorrelation;

        /// <summary>
        /// A Helper object used to compute moving averages.
        /// </summary>
        MovingAverage movingAverage;

        /// <summary>
        /// A Helper object used to dive signals up into frames.
        /// </summary>
        FrameCutter frameCutter;

        #endregion

        #region DegaraBeatTracking

        /// <summary>
        /// Magic number
        /// </summary>
        float alpha;

        /// <summary>
        /// Magic number
        /// </summary>
        float sigma_ibi;

        /// <summary>
        /// Magic number
        /// </summary>
        int numberStates;

        /// <summary>
        /// The resolution of the onset detection function.
        /// </summary>
        float resolutionODF;

        /// <summary>
        /// The number of samples in the onsetDetections signal.
        /// </summary>
        int numberFrames;

        #endregion

        /// <summary>
        /// 
        /// Note that the input values of the onset detection functions must be non-negative otherwise an exception is thrown. Parameter \"maxTempo\" should be 20bpm larger than \"minTempo\", otherwise an exception is thrown.
        /// </summary>
        /// <param name="sampleRateODF">
        /// The sampling rate of the onset detection function [Hz].
        /// This is equivalent to the Signal Sample rate / the ODF hop size.
        /// </param>
        /// <param name="resample">The amount of resampling to perform.</param>
        /// <param name="maxTempo">The maximum tempo to look for.</param>
        /// <param name="minTempo">The minimum tempo to look for.</param>
        public TempoTapDegara(float sampleRateODF = 44100f / 512f, Resample resample = Resample.None, float maxTempo = 240, float minTempo = 40)
        {
            this.resample = (int)resample;
            this.sampleRateODF = sampleRateODF * this.resample;

            this.frameDurationODF = 5.944308390022676f;
            this.alpha = 0.5f;
            this.sigma_ibi = 0.025f;

            // ------- M. Davies --------
            // Use hopsize of 1.5 secs, frame size of 6 secs to cut ODF frames. We want
            // to estimate some period for each frame, therefore, the maximum period we
            // can find is limited to the hopsize and corresponds to 40 BPM. Having a
            // frame size 4 times larger, we can take into account the periodicities on
            // integer multiples, using for this purpose up to 4 comb filters in the bank.
            this.hopDurationODF = this.frameDurationODF / numberCombs;
            this.frameSizeODF = (int)Math.Round(this.frameDurationODF * this.sampleRateODF);
            this.hopSizeODF = this.frameSizeODF / numberCombs;

            // Smoothing window size of 0.2s: 0.1s advance + 0.1s delay
            this.smoothingWindowHalfSize = (int)(0.1 * this.sampleRateODF);
            this.movingAverage = new MovingAverage(this.smoothingWindowHalfSize * 2 + 1);
            this.autocorrelation = new AutoCorrelation(NormalizeType.Unbiased);

            this.CreateTempoPreferenceCurve(minTempo, maxTempo);

            // 0-th element in autocorrelation vector will corresponds to the period of 1.
            // Min value for the 'region' variable is -3 => we will compute starting from i
            // the 3-rd index, which corresponds to the period of 4, until period of 127 =
            // (512-3) / 4 = 127 ODF samples (or until 120 as in matlab code).
            this.periodMinIndex = numberCombs - 1;
            this.periodMaxIndex = (this.frameSizeODF - this.periodMinIndex) / numberCombs - 1;

            this.periodMaxUserIndex = (int)Math.Ceiling(60f / minTempo * this.sampleRateODF) - 1;
            this.periodMinUserIndex = (int)Math.Floor(60f / maxTempo * this.sampleRateODF) - 1;

            this.periodMinUserIndex = Math.Min(this.hopSizeODF - 1, this.periodMinUserIndex);
            this.periodMaxUserIndex = Math.Min(this.hopSizeODF - 1, this.periodMaxUserIndex);

            this.CreateViterbiTransitionMatrix();

            this.resolutionODF = 1f / this.sampleRateODF;
        }


        /// <summary>
        ///   Tempo preference weights (Rayleigh distribution) with a peak at 120 BPM,
        ///   equal to pow(43, 2) with the default ODF sample rate (44100./512).
        ///   Maximum period of ODF to consider (period of 512 ODF samples with the
        ///   default settings) correspond to 512 * 512. / 44100. = ~6 secs
        /// </summary>
        private void CreateTempoPreferenceCurve(float minTempo, float maxTempo)
        {
            float tempoPreference = (minTempo + maxTempo) / 2f;
            float rayparam2 = (float)Math.Pow(Math.Round(60f * this.sampleRateODF / tempoPreference), 2);
            int maxPeriod = this.hopSizeODF;
            this.tempoWeights = new float[maxPeriod];
            for(int i = 0; i < maxPeriod; i++)
            {
                int tau = i + 1;
                this.tempoWeights[i] = (float)(tau / rayparam2 * Math.Exp(-0.5 * tau * tau / rayparam2));
            }

            MathHelpers.NormalizeSum(ref this.tempoWeights);
        }

        /// <summary>
        ///   Prepare a transition matrix for Viterbi algorithm: it is a this.hopSizeODF x
        ///   this.hopSizeODF matrix, where each column i consists of a gaussian centered
        ///   at i, with stddev=8 by default (i.e., when this.hopSizeODF=128), and leave
        ///   columns before 28th and after 108th zeroed, as well as the lines before
        ///   28th and after 108th. Paper: informal tests revealed that stddev parameter
        ///   can vary by a factor of 2 without altering the overall performance of beat
        ///   tracker.
        /// </summary>
        private void CreateViterbiTransitionMatrix()
        {
            this.transitionsViterbi = new List<List<float>>();
            for (int i = 0; i < this.hopSizeODF; i++)
            {
                this.transitionsViterbi.Add(new List<float>());
                for (int j = 0; j < this.hopSizeODF; j++)
                {
                    this.transitionsViterbi[i].Add(0);
                }
            }

            float scale = this.sampleRateODF / (44100f / 512f);

            // each sequet column contains a gaussian shifted by 1 line
            float[] gaussian = this.GaussianPDF(8 * scale, 1, scale);

            int minIndex = (int)Math.Floor(28 * scale) - 1;
            int maxIndex = (int)Math.Ceiling(108 * scale) - 1;
            int gaussianMean = gaussian.Length / 2;

            for(int i =minIndex; i <= maxIndex; i++)
            {
                for(int j = i - gaussianMean; j <= i+gaussianMean; j++)
                {
                    if (j >= minIndex && j <= maxIndex)
                    {
                        this.transitionsViterbi[i][j] = gaussian[j - (i - gaussianMean)];
                    }
                }
            }
        }

        /// <summary>
        /// Create a gaussian with the specified parameters.
        /// </summary>
        /// <param name="gaussianStd">The standard deviation of the gaussian.</param>
        /// <param name="step">The step of the gaussian</param>
        /// <param name="scale">The scale of the gaussian.</param>
        /// <returns></returns>
        private float[] GaussianPDF(float gaussianStd, float step, float scale)
        {
            int gaussianSize = (int)(2 * Math.Ceiling(4 * gaussianStd / step) + 1);
            int gaussianMean = gaussianSize / 2;
            float[] gaussian = new float[gaussianSize];

            float term1 = (float)(1f / (gaussianStd * Math.Sqrt(2 * Math.PI)));
            float term2 = (float)(-2 * Math.Pow(gaussianStd, 2));
            for (int i = 0; i <= gaussianMean; i++)
            {
                gaussian[i] = (float)(term1 * Math.Exp(Math.Pow((i - gaussianMean) * step, 2) / term2) * scale);
                if (gaussian[i] < 1e-12)
                {
                    gaussian[i] = 0;
                }

                gaussian[gaussianSize - 1 - i] = gaussian[i];
            }

            return gaussian;
        }

        /// <summary>
        /// Compute the tick locations of a beat given the onset detection function output.
        /// </summary>
        /// <param name="onsetDetections">
        /// The input frame-wise vector of onset detection values.
        /// </param>
        /// <returns>
        /// The list of resulting ticks.
        /// </returns>
        public float[] Compute(float[] onsetDetections)
        {
            for(int i = 0; i < onsetDetections.Length; i++)
            {
                if (onsetDetections[i] < 0)
                {
                    throw new Exception($"TempoTapDegara: onset detection values must be non-negative.");
                }
            }

            // Clone the input array so we accidentally modify it
            float[] odfClone = (float[])onsetDetections.Clone();
            MathHelpers.Normalize(ref odfClone);
            if (this.resample > 1 && odfClone.Length > 1)
            {
                float[] temp = new float[(odfClone.Length - 1) * this.resample + 1];
                for( int i = 0; i < odfClone.Length-1; i++)
                {
                    float delta = (odfClone[i + 1] - odfClone[i]) / this.resample;
                    for(int j = 0; j < resample; j++)
                    {
                        temp[i * this.resample + j] = odfClone[i] + delta * j;
                    }
                }

                odfClone = temp;
            }

            this.ComputeBeatPeriodsDavies(odfClone, out List<float> beatPeriods, out List<float> beatEndPositions);
            return this.ComputeBeatsDegara(odfClone, beatPeriods, beatEndPositions);
        }

        /// <summary>
        /// Compute the beat periods using Davies' algorithm
        /// </summary>
        /// <param name="onsetDetections">
        /// The onset detections
        /// </param>
        /// <param name="beatPeriods">
        /// The resulting beat periods.
        /// </param>
        /// <param name="beatEndPositions">
        /// The resulting beat end positions.
        /// </param>
        private void ComputeBeatPeriodsDavies(float[] onsetDetections, out List<float> beatPeriods, out List<float> beatEndPositions)
        {
            this.AdaptiveThreshold(ref onsetDetections, this.smoothingWindowHalfSize);

            // Tempo estimation:
            // - Split detection function into overlapping frames.
            // - Compute autocorrelation (ACF) for each frame with bias correction.
            // - Weight it by the tempo preference curve (Rayleigh distrubution).
            List<float[]> observations = new List<float[]>();
            float observationsMax = 0;
            float[] frame;
            float[] frameACF;
            float[] frameACFNormalized = new float[this.hopSizeODF];

            this.frameCutter = new FrameCutter(onsetDetections, this.frameSizeODF, this.hopSizeODF, startFromZero: true);
            while(true)
            {
                frame = this.frameCutter.GetNextFrame();
                if (frame == null || frame.Length == 0)
                {
                    break;
                }

                frameACF = this.autocorrelation.Compute(frame);
                // To accout for poor resolution of ACF at short lags, each comb element has
                // width proportional to its relationship to the underlying periodicity, and
                // its height is normalized by its width.
                for (int comb = 1; comb <= numberCombs; comb++)
                {
                    int width = 2 * comb - 1;
                    for (int region = 1 - comb; region <= comb - 1; region++)
                    {
                        for (int period = this.periodMinIndex; period <= this.periodMaxIndex; ++period)
                        {
                            frameACFNormalized[period] += this.tempoWeights[period] * frameACF[(period + 1) * comb - 1 + region] / width;
                        }
                    }
                }

                // Apply adaptive threshold. It is not mentioned in the paper, but is taken
                // from matlab code by M.Davies (including the smoothing size). The
                // implemented smoothing does not exactly match the one in matlab code,
                // howeer, the evaluation results were found very close.
                this.AdaptiveThreshold(ref frameACFNormalized, 8);

                // zero weights for periods out of the user-specified range
                for(int i = 0; i < this.periodMinUserIndex + 1; i++)
                {
                    frameACFNormalized[i] = 0;
                }

                for(int i = this.periodMaxUserIndex + 1; i < frameACFNormalized.Length; i++)
                {
                    frameACFNormalized[i] = 0;
                }

                MathHelpers.NormalizeSum(ref frameACFNormalized);
                observations.Add(frameACFNormalized);

                float frameMax = frameACFNormalized.Max();
                if (frameMax > observationsMax)
                {
                    observationsMax = frameMax;
                }
            }

            this.numberFramesODF = observations.Count;
            // Add Noise
            Random rand = new Random();
            for(int t = 0; t < this.numberFramesODF; t++)
            {
                for(int i = 0; i < this.hopSizeODF; i++)
                {
                    observations[t][i] += (float)(0.0001 * observationsMax * rand.NextDouble());
                }
            }

            // Find Viterbi path (ODF-frame-wise list of indices of the estimated periods;
            // Zero index corresponds to the beat period of 1 ODF frame hopsize
            int[] path = this.FindViterbiPath(this.tempoWeights, this.transitionsViterbi, observations);
            beatPeriods = new List<float>(this.numberFramesODF);
            beatEndPositions = new List<float>(this.numberFramesODF);
            for(int t = 0; t < this.numberFramesODF; t++)
            {
                beatPeriods.Add((path[t] + 1f) / this.sampleRateODF);
                beatEndPositions.Add((t + 1f) * this.hopDurationODF);
            }
        }

        /// <summary>
        /// Find the most-probable (Viterbi) path through the HMM state trellis.
        /// delta(j,t) = prob. of the best sequence of length t-1 and then going to state j, and O(1:t)
        /// psi(j,t) = the best predecessor state, given that we ended up in state j at t
        /// </summary>
        /// <param name="prior">
        /// prior(i) = Pr(Q(1) = i)
        /// </param>
        /// <param name="transitionMatrix">
        /// transmat(i,j) = Pr(Q(t+1)=j | Q(t)=i)
        /// </param>
        /// <param name="observations">
        /// observations(i,t) = Pr(y(t) | Q(t)=i)
        /// </param>
        /// <returns>
        /// path(t) = q(t), where q1 ... qT is the argmax of the above expression.
        /// </returns>
        private int[] FindViterbiPath(float[] prior, List<List<float>> transitionMatrix, List<float[]> observations)
        {
            int numberPeriods = prior.Length;
            List<List<float>> delta = new List<List<float>>();
            List<List<int>> psi = new List<List<int>>();

            List<float> deltaNew = new List<float>(numberPeriods);
            List<int> psiNew = new List<int>(numberPeriods);
            for (int i = 0; i < numberPeriods; i++)
            {
                deltaNew.Add(prior[i] * observations[0][i]);
                psiNew.Add(0);
            }

            MathHelpers.NormalizeSum(ref deltaNew);
            delta.Add(new List<float>(deltaNew));
            psi.Add(new List<int>(psiNew));

            float[] temp = new float[numberPeriods];
            for(int t = 1; t < this.numberFramesODF; t++)
            {
                for(int j = 0; j < numberPeriods; j++)
                {
                    for(int i = 0; i < numberPeriods; i++)
                    {
                        // weighten delta for a previous frame by vector from the transitionMatrix
                        temp[i] = delta[delta.Count - 1][i] * transitionMatrix[j][i];
                    }

                    float tempMax = temp.Max();
                    int iMax = temp.IndexOf(tempMax);
                    deltaNew[j] = tempMax * observations[t][j];
                    psiNew[j] = iMax;
                }

                MathHelpers.NormalizeSum(ref deltaNew);
                delta.Add(new List<float>(deltaNew));
                psi.Add(new List<int>(psiNew));
            }

            // Track the path backwards in time
            int[] path = new int[this.numberFramesODF];
            path[path.Length - 1] = MathHelpers.ArgMax<float>(delta[delta.Count - 1]);
            if (this.numberFramesODF >= 2)
            {
                for (int t = this.numberFramesODF - 2; t >= 0; t--)
                {
                    path[t] = psi[t + 1][path[t+1]];
                }
            }

            return path;
        }

        /// <summary>
        /// Implementation of Degara's beat tracking using a probabilitic framework
        /// (Hidden Markov Model). Tempo estimations throughout the track are assumed
        /// to be computed from the algorithm by M. Davies.
        /// </summary>
        /// <param name="onsetDetections">The onset detection function output.</param>
        /// <param name="beatPeriods">The beat periods computed by M.Davies' algorithm.</param>
        /// <param name="beatEndPositions">The beat end positions computed by M.Davies' algorithm.</param>
        /// <returns>
        /// The tick locations for the beats.
        /// </returns>
        private float[] ComputeBeatsDegara(float[] onsetDetections, List<float> beatPeriods, List<float> beatEndPositions)
        {
            // avoid zeros to void log(0) errors
            for(int i = 0; i < onsetDetections.Length; i++)
            {
                if (onsetDetections[i] == 0)
                {
                    onsetDetections[i] = float.Epsilon;
                }
            }

            float periodMax = beatPeriods.Max();
            float ibiMax = periodMax + 3 * this.sigma_ibi;
            List<float> ibi = new List<float>();
            for(float t = this.resolutionODF; t <= ibiMax; t += this.resolutionODF)
            {
                ibi.Add(t);
            }

            this.numberStates = ibi.Count;

            // Compute transition matrix from the inter-beat-interval distribution
            // according to the tempo estimates. Transition matrix is unique for each beat period.
            Dictionary<float, List<List<float>>> transitionMatrix = new Dictionary<float, List<List<float>>>();
            float[] gaussian = this.GaussianPDF(this.sigma_ibi, this.resolutionODF, 0.01f / this.resample);
            float[] ibiPDF = new float[this.numberStates];

            for (int i = 0; i < beatPeriods.Count; i++)
            {
                if (!transitionMatrix.ContainsKey(beatPeriods[i]))
                {
                    // Shift gaussian vector to be centered at beatPEriods[i] secs which is equivalent to round(beatPeriods[i] / this.resolutionODF) samples.
                    int shift = (int)(gaussian.Length / 2 - Math.Round(beatPeriods[i] / this.resolutionODF - 1));
                    for (int j = 0; j < numberStates; j++)
                    {
                        int j_new = j + shift;
                        ibiPDF[j] = j_new < 0 || j_new >= gaussian.Length ? 0 : gaussian[j_new];
                    }

                    transitionMatrix[beatPeriods[i]] = this.ComputeHMMTransitionMatrix(ibiPDF);
                }
            }

            // Compute observation likelihoods for each HMM state
            List<List<float>> biy = new List<List<float>>(this.numberStates);

            // Treat ODF as probability, normalize to 0.99 to avoid numerical errors
            this.numberFrames = onsetDetections.Length;
            float[] beatProbability = new float[this.numberFrames];
            float[] noBeatProbability = new float[this.numberFrames];
            for(int i = 0; i < this.numberFrames; i++)
            {
                beatProbability[i] = 0.99f * onsetDetections[i];
                noBeatProbability[i] = 1f - beatProbability[i];
                beatProbability[i] = (1f - this.alpha) * (float)Math.Log(beatProbability[i]);
                noBeatProbability[i] = (1f - this.alpha) * (float)Math.Log(noBeatProbability[i]);
            }

            biy.Add(new List<float>(beatProbability));
            for(int i = 0; i < this.numberStates - 1; i++)
            {
                biy.Add(new List<float>(noBeatProbability));
            }

            // Decoding
            List<int> stateSequence = this.DecodeBeats(transitionMatrix, beatPeriods, beatEndPositions, biy);
            List<float> ticks = new List<float>();
            for(int i = 0; i < stateSequence.Count; i++)
            {
                if (stateSequence[i] == 0)
                {
                    // Beat detected!
                    ticks.Add(i * this.resolutionODF);
                }
            }

            return ticks.ToArray();
        }

        /// <summary>
        /// Decode the beats
        /// </summary>
        /// <param name="transitionMatrix"></param>
        /// <param name="beatPeriods"></param>
        /// <param name="beatEndPositions"></param>
        /// <param name="biy"></param>
        /// <returns></returns>
        private List<int> DecodeBeats(Dictionary<float, List<List<float>>> transitionMatrix, List<float> beatPeriods, List<float> beatEndPositions, List<List<float>> biy)
        {
            int currentIndex = 0;
            int[,] stateBacktracking = new int[this.numberStates, this.numberFrames];

            // HMM Cost for each state for the current time.
            List<float> cost = new List<float>(this.numberStates);
            List<float> costOld = new List<float>(this.numberStates);
            for (int i = 0; i < this.numberStates; i++)
            {
                cost.Add(float.MaxValue);
                costOld.Add(float.MaxValue);
            }

            cost[0] = 0;
            costOld[0] = 0;
            float[] diff = new float[this.numberStates];

            // Dynamic programming!
            for(int t = 0; t < numberFrames; t++)
            {
                // evaluate transitions from any stat to state event (state 0)

                // look for the minimum cost
                for(int i = 0; i < this.numberStates; i++)
                {
                    diff[i] = costOld[i] - transitionMatrix[beatPeriods[currentIndex]][i][0];
                }

                int bestState = MathHelpers.ArgMin(diff);
                float bestPath = diff[bestState];

                if(bestPath == float.MaxValue)
                {
                    bestState = -1;
                }

                // Save beset transitions information for backtracking
                stateBacktracking[0, t] = bestState;
                // Update cost; the only possible transition is from state to state+1
                cost[0] = -biy[0][t] + bestPath;
                for(int state = 1; state < this.numberStates; state++)
                {
                    cost[state] = costOld[state - 1] - transitionMatrix[beatPeriods[currentIndex]][state - 1][state] - biy[state][t];
                    stateBacktracking[state, t] = state - 1;
                }

                // Update cost at t-1
                costOld = new List<float>(cost);

                // Find the transition matrix corresponding to the next frame
                if (t+1 < this.numberFrames)
                {
                    float currentTime = (t + 1) * this.resolutionODF;
                    for(int i = currentIndex + 1; i < beatEndPositions.Count && beatEndPositions[i] <= currentTime; i++)
                    {
                        currentIndex = i;
                    }
                }
            }

            // Decide which of the final states is most probable
            int finalState = MathHelpers.ArgMin(cost);

            // Backtrace through the model
            int[] sequenceStates = new int[this.numberFrames];
            sequenceStates[sequenceStates.Length - 1] = finalState;
            if (this.numberFrames >= 2)
            {
                for(int t = this.numberFrames - 2; t >= 0; t--)
                {
                    sequenceStates[t] = stateBacktracking[sequenceStates[t + 1], t + 1];
                }
            }

            return sequenceStates.ToList();
        }

        /// <summary>
        /// Some magic
        /// </summary>
        /// <param name="ibiPDF"></param>
        /// <returns></returns>
        private List<List<float>> ComputeHMMTransitionMatrix(float[] ibiPDF)
        {
            List<List<float>> transitions = new List<List<float>>();
            // fill in result matrix with zeros
            for(int i = 0; i < this.numberStates; i++)
            {
                transitions.Add(new List<float>());
                for(int j = 0; j < this.numberStates; j++)
                {
                    transitions[i].Add(0);
                }
            }

            // Estimate transition probabilities
            transitions[0][0] = ibiPDF[0];
            transitions[0][1] = 1 - transitions[0][0];
            for(int i = 1; i < this.numberStates; i++)
            {
                float[] temp = new float[i];
                for(int k = 0; k < i; k++)
                {
                    temp[k] = (float)Math.Log(transitions[k][k + 1]);
                }

                transitions[i][0] = (float)Math.Exp(Math.Log(ibiPDF[i]) - temp.Sum());
                if (transitions[i][0] < 0 || transitions[i][0] > 1)
                {
                    if (transitions[i][0] < 0)
                    {
                        transitions[i][0] = 0;
                    }
                    else
                    {
                        transitions[i][0] = 1;
                    }
                }

                if (i + 1 < this.numberStates)
                {
                    transitions[i][i + 1] = 1 - transitions[i][0];
                }
            }

            // NB: work in log space to avoid numerical issues
            for(int i = 0; i < this.numberStates; i++)
            {
                for(int j = 0; j < this.numberStates; j++)
                {
                    transitions[i][j] = (float)Math.Log(transitions[i][j]) * this.alpha;
                }
            }

            return transitions;
        }

        /// <summary>
        /// Adaptive moving average threshold to emphasize the strongest and discard the
        /// least significant peaks. Subtract the adaptive mean, and half-wave rectify
        /// the output, setting any negative valued elements to zero.
        ///
        /// Align filter output for symmetrical averaging, and we want the filter to
        /// return values on the edges as the averager output computed at these
        /// positions to avoid smoothing to zero.
        /// </summary>
        /// <param name="array">The array to threshold</param>
        /// <param name="smoothingHalfSize">The size of the moving averager</param>
        private void AdaptiveThreshold(ref float[] array, int smoothingHalfSize)
        {


            List<float> temp = new List<float>(array);
            for(int i = 0; i < smoothingHalfSize; i++)
            {
                temp.Insert(0, temp[0]);
                temp.Add(temp[temp.Count - 1]);
            }

            List<float> smoothed = new List<float>(this.movingAverage.Compute(temp.ToArray()));
            for(int i = 0; i < array.Length; i++)
            {
                array[i] -= smoothed[2 * smoothingHalfSize + i];
                if (array[i] < 0)
                {
                    array[i] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Potential resampling ammounts.
    /// </summary>
    public enum Resample
    {
        None = 1,
        X2 = 2,
        X3 = 3,
        X4 = 4
    }
}
