using CNTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.CNTKModels
{
    public static class LSTMSequenceClassifier
    {
        public static void Train(DeviceDescriptor device, string ctfFile, bool useBuiltInRNN = false)
        {
            const int inputDim = 40;
            const int cellDim = inputDim * 5;
            const int hiddenDim = 2;
            const int embeddingDim = 3;
            // [Blank, Green, Red, Yellow, Blue, Orange, Open]
            const int numOutputClasses = 1;

            // build the model
            string featuresName = "features";
            Variable features   = Variable.InputVariable(new int[] { inputDim }, DataType.Float, featuresName, null, false);
            string labelsName   = "labels";
            Variable labels     = Variable.InputVariable(new int[] { numOutputClasses }, DataType.Float, labelsName, new List<Axis>() { Axis.DefaultBatchAxis() }, false, true);

            Function classifierOutput = null;
            if (useBuiltInRNN)
            {
                classifierOutput = LSTMSequenceClassifierNet(features, numOutputClasses, embeddingDim, hiddenDim, cellDim, device, "classifierOutput");
            }
            else
            {
                Parameter weights = new Parameter(new int[] { NDShape.InferredDimension, NDShape.InferredDimension }, DataType.Float, CNTKLib.GlorotUniformInitializer(CNTKLib.DefaultParamInitScale, CNTKLib.SentinelValueForInferParamInitRank, CNTKLib.SentinelValueForInferParamInitRank, 1));
                Function rnn = CNTKLib.Tanh(CNTKLib.OptimizedRNNStack(features, weights, 512, 40, true, "lstm"));
                Function dense = TestHelper.Dense(rnn, 40, device, Activation.ReLU);
                classifierOutput = TestHelper.Dense(dense, 1, device, Activation.Sigmoid, "classifierOutput");
            }

            //Function trainingLoss = CNTKLib.BinaryCrossEntropy(classifierOutput, labels, "lossFunction");
            Function trainingLoss = CNTKLib.BinaryCrossEntropy(classifierOutput, labels, "lossFunction");
            Function prediction = CNTKLib.SquaredError(classifierOutput, labels,  "classificationError");

            // prepare training data
            IList<StreamConfiguration> streamConfigurations = new StreamConfiguration[]
            {
                new StreamConfiguration(featuresName, inputDim), new StreamConfiguration(labelsName, numOutputClasses)
            };

            MinibatchSource minibatchSource = MinibatchSource.TextFormatMinibatchSource(
                ctfFile, streamConfigurations,
                MinibatchSource.InfinitelyRepeat, false);

            StreamInformation featureStreamInfo = minibatchSource.StreamInfo(featuresName);
            StreamInformation labelStreamInfo = minibatchSource.StreamInfo(labelsName);

            // prepare for training
            uint minibatchSize = 20000;
            AdditionalLearningOptions opts = new AdditionalLearningOptions();
            opts.l2RegularizationWeight = 0.001;
            TrainingParameterScheduleDouble learningRatePerSample = new TrainingParameterScheduleDouble(0.0001, minibatchSize);
            TrainingParameterScheduleDouble momentumTimeConstant = CNTKLib.MomentumAsTimeConstantSchedule(CNTKLib.MomentumFromTimeConstant(10.0));
            IList<Learner> parameterLearners = new List<Learner>()
            {
                Learner.MomentumSGDLearner(classifierOutput.Parameters(), learningRatePerSample, momentumTimeConstant, true, opts)
            };

            Trainer trainer = Trainer.CreateTrainer(classifierOutput, trainingLoss, prediction, parameterLearners);

            // train the model
            int outputFrequencyInMinibatches = 10;
            float[] errorPercentages = new float[outputFrequencyInMinibatches];
            int index = 0;
            int miniBatchCount = 0;
            int numEpochs = 100000;
            while (numEpochs > 0)
            {
                UnorderedMapStreamInformationMinibatchData minibatchData = minibatchSource.GetNextMinibatch(minibatchSize, device);

                Dictionary<Variable, MinibatchData> arguments = new Dictionary<Variable, MinibatchData>
                {
                    { features, minibatchData[featureStreamInfo] },
                    { labels, minibatchData[labelStreamInfo] }
                };

                trainer.TrainMinibatch(arguments, device);
                TestHelper.PrintTrainingProgress(trainer, miniBatchCount++, outputFrequencyInMinibatches);

                errorPercentages[index] = (float)trainer.PreviousMinibatchEvaluationAverage();
                index = (index + 1) % outputFrequencyInMinibatches;
                if(index == 0)
                {
                    Console.WriteLine($"Average ErrorPercentage = {errorPercentages.Average()} MinErrorPercentage = {errorPercentages.Min()} MaxErrorPercentage = {errorPercentages.Max()}");
                }

                // Because minibatchSource is created with MinibatchSource.InfinitelyRepeat, 
                // batching will not end. Each time minibatchSource completes an sweep (epoch),
                // the last minibatch data will be marked as end of a sweep. We use this flag
                // to count number of epochs.
                if (TestHelper.MiniBatchDataIsSweepEnd(minibatchData.Values))
                {
                    numEpochs--;
                }
            }

            string modelFile = @"C:\test\LSTMOnset.model";
            classifierOutput.Save(modelFile);

            // validate the model
            MinibatchSource validationMinibatchSource = MinibatchSource.TextFormatMinibatchSource(
                ctfFile, streamConfigurations,
                MinibatchSource.InfinitelyRepeat, false);
            TestHelper.ValidateLSTMModelWithMiniBatchSource(modelFile, validationMinibatchSource, featuresName, labelsName, device);
        }

        public static void ValidateModelFile(string modelFile, string ctfFile, DeviceDescriptor device)
        {
            const int inputDim = 40 * 9;
            const int numOutputClasses = 2;

            // build the model
            string featuresName = "features";
            string labelsName = "labels";

            // prepare training data
            IList<StreamConfiguration> streamConfigurations = new StreamConfiguration[]
            {
                new StreamConfiguration(featuresName, inputDim), new StreamConfiguration(labelsName, numOutputClasses)
            };

            // validate the model
            MinibatchSource validationMinibatchSource = MinibatchSource.TextFormatMinibatchSource(
                ctfFile, streamConfigurations,
                MinibatchSource.InfinitelyRepeat, false);
            TestHelper.ValidateLSTMModelWithMiniBatchSource(modelFile, validationMinibatchSource, featuresName, labelsName, device);
        }

        /// <summary>
        /// Build a one direction recurrent neural network (RNN) with long-short-term-memory (LSTM) cells.
        /// http://colah.github.io/posts/2015-08-Understanding-LSTMs/
        /// </summary>
        /// <param name="input">the input variable</param>
        /// <param name="numOutputClasses">number of output classes</param>
        /// <param name="embeddingDim">dimension of the embedding layer</param>
        /// <param name="LSTMDim">LSTM output dimension</param>
        /// <param name="cellDim">cell dimension</param>
        /// <param name="device">CPU or GPU device to run the model</param>
        /// <param name="outputName">name of the model output</param>
        /// <returns>the RNN model</returns>
        static Function LSTMSequenceClassifierNet(Variable input, int numOutputClasses, int embeddingDim, int LSTMDim, int cellDim, DeviceDescriptor device,
            string outputName)
        {
            Function embeddingFunction = Embedding(input, embeddingDim, device);
            Func<Variable, Function> pastValueRecurrenceHook = (x) => CNTKLib.PastValue(x);
            Function LSTMFunction = LSTMPComponentWithSelfStabilization<float>(
                embeddingFunction,
                new int[] { LSTMDim },
                new int[] { cellDim },
                pastValueRecurrenceHook,
                pastValueRecurrenceHook,
                device).Item1;
            Function thoughtVectorFunction = CNTKLib.SequenceLast(LSTMFunction);

            return CNTKLib.ReLU(TestHelper.FullyConnectedLinearLayer(thoughtVectorFunction, numOutputClasses, device));
        }

        private static Function Embedding(Variable input, int embeddingDim, DeviceDescriptor device)
        {
            System.Diagnostics.Debug.Assert(input.Shape.Rank == 1);
            int inputDim = input.Shape[0];
            var embeddingParameters = new Parameter(new int[] { embeddingDim, inputDim }, DataType.Float, CNTKLib.GlorotUniformInitializer(), device);
            return CNTKLib.Times(embeddingParameters, input);
        }

        static Tuple<Function, Function> LSTMPComponentWithSelfStabilization<ElementType>(
            Variable input,
            NDShape outputShape, NDShape cellShape,
            Func<Variable, Function> recurrenceHookH,
            Func<Variable, Function> recurrenceHookC,
            DeviceDescriptor device)
        {
            var dh = Variable.PlaceholderVariable(outputShape, input.DynamicAxes);
            var dc = Variable.PlaceholderVariable(cellShape, input.DynamicAxes);

            var LSTMCell = LSTMPCellWithSelfStabilization<ElementType>(input, dh, dc, device);
            var actualDh = recurrenceHookH(LSTMCell.Item1);
            var actualDc = recurrenceHookC(LSTMCell.Item2);

            // Form the recurrence loop by replacing the dh and dc placeholders with the actualDh and actualDc
            (LSTMCell.Item1).ReplacePlaceholders(new Dictionary<Variable, Variable> { { dh, actualDh }, { dc, actualDc } });

            return new Tuple<Function, Function>(LSTMCell.Item1, LSTMCell.Item2);
        }

        static Tuple<Function, Function> LSTMPCellWithSelfStabilization<ElementType>(Variable input, Variable prevOutput, Variable prevCellState, DeviceDescriptor device)
        {
            int outputDim = prevOutput.Shape[0];
            int cellDim = prevCellState.Shape[0];

            bool isFloatType = typeof(ElementType).Equals(typeof(float));
            DataType dataType = isFloatType ? DataType.Float : DataType.Double;

            Func<int, Parameter> createBiasParam;
            if (isFloatType)
                createBiasParam = (dim) => new Parameter(new int[] { dim }, 0.01f, device, "");
            else
                createBiasParam = (dim) => new Parameter(new int[] { dim }, 0.01, device, "");

            uint seed2 = 1;
            Func<int, Parameter> createProjectionParam = (oDim) => new Parameter(new int[] { oDim, NDShape.InferredDimension },
                    dataType, CNTKLib.GlorotUniformInitializer(1.0, 1, 0, seed2++), device);

            Func<int, Parameter> createDiagWeightParam = (dim) =>
                new Parameter(new int[] { dim }, dataType, CNTKLib.GlorotUniformInitializer(1.0, 1, 0, seed2++), device);

            Function stabilizedPrevOutput = Stabilize<ElementType>(prevOutput, device);
            Function stabilizedPrevCellState = Stabilize<ElementType>(prevCellState, device);

            Func<Variable> projectInput = () =>
                createBiasParam(cellDim) + (createProjectionParam(cellDim) * input);

            // Input gate
            Function it =
                CNTKLib.Sigmoid(
                    (Variable)(projectInput() + (createProjectionParam(cellDim) * stabilizedPrevOutput)) +
                    CNTKLib.ElementTimes(createDiagWeightParam(cellDim), stabilizedPrevCellState));
            Function bit = CNTKLib.ElementTimes(
                it,
                CNTKLib.Tanh(projectInput() + (createProjectionParam(cellDim) * stabilizedPrevOutput)));

            // Forget-me-not gate
            Function ft = CNTKLib.Sigmoid(
                (Variable)(
                        projectInput() + (createProjectionParam(cellDim) * stabilizedPrevOutput)) +
                        CNTKLib.ElementTimes(createDiagWeightParam(cellDim), stabilizedPrevCellState));
            Function bft = CNTKLib.ElementTimes(ft, prevCellState);

            Function ct = (Variable)bft + bit;

            // Output gate
            Function ot = CNTKLib.Sigmoid(
                (Variable)(projectInput() + (createProjectionParam(cellDim) * stabilizedPrevOutput)) +
                CNTKLib.ElementTimes(createDiagWeightParam(cellDim), Stabilize<ElementType>(ct, device)));
            Function ht = CNTKLib.ElementTimes(ot, CNTKLib.Tanh(ct));

            Function c = ct;
            Function h = (outputDim != cellDim) ? (createProjectionParam(outputDim) * Stabilize<ElementType>(ht, device)) : ht;

            return new Tuple<Function, Function>(h, c);
        }

        static Function Stabilize<ElementType>(Variable x, DeviceDescriptor device)
        {
            bool isFloatType = typeof(ElementType).Equals(typeof(float));
            Constant f, fInv;
            if (isFloatType)
            {
                f = Constant.Scalar(4.0f, device);
                fInv = Constant.Scalar(f.DataType, 1.0 / 4.0f);
            }
            else
            {
                f = Constant.Scalar(4.0, device);
                fInv = Constant.Scalar(f.DataType, 1.0 / 4.0f);
            }

            var beta = CNTKLib.ElementTimes(
                fInv,
                CNTKLib.Log(
                    Constant.Scalar(f.DataType, 1.0) +
                    CNTKLib.Exp(CNTKLib.ElementTimes(f, new Parameter(new NDShape(), f.DataType, 0.99537863 /* 1/f*ln (e^f-1) */, device)))));
            return CNTKLib.ElementTimes(beta, x);
        }
    }
}
