using CNTK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.CNTKModels
{
    public class RecurrentConvolutionalNeuralNetwork
    {
        /// <summary>
        /// Train and evaluate a image classifier for MNIST data.
        /// </summary>
        /// <param name="device">CPU or GPU device to run training and evaluation</param>
        /// <param name="useConvolution">option to use convolution network or to use multilayer perceptron</param>
        /// <param name="forceRetrain">whether to override an existing model.
        /// if true, any existing model will be overridden and the new one evaluated. 
        /// if false and there is an existing model, the existing model is evaluated.</param>
        public static void TrainAndEvaluate(DeviceDescriptor device, string ctfFile, bool forceRetrain)
        {
            int featureLength = 40;
            int windowSize = 9;
            var featureStreamName = "features";
            var labelsStreamName = "labels";
            var classifierName = "classifierOutput";
            Function classifierOutput;
            int[] imageDim = new int[] { windowSize, featureLength, 1 };
            int imageSize = featureLength * windowSize;
            int numClasses = 2;

            IList<StreamConfiguration> streamConfigurations = new StreamConfiguration[]
                { new StreamConfiguration(featureStreamName, imageSize), new StreamConfiguration(labelsStreamName, numClasses) };

            string modelFile = @"C:\test\Temp\Convolution.model";

            // If a model already exists and not set to force retrain, validate the model and return.
            if (File.Exists(modelFile) && !forceRetrain)
            {
                var minibatchSourceExistModel = MinibatchSource.TextFormatMinibatchSource(ctfFile, streamConfigurations, MinibatchSource.InfinitelyRepeat, false);
                TestHelper.ValidateModelWithMinibatchSource(modelFile, minibatchSourceExistModel, imageDim, numClasses, featureStreamName, labelsStreamName, classifierName, device);
                return;
            }

            // build the network
            var input = CNTKLib.InputVariable(imageDim, DataType.Float, featureStreamName);
            classifierOutput = RecurrentCNN(input, device, imageDim);

            var labels = CNTKLib.InputVariable(new int[] { numClasses }, DataType.Float, labelsStreamName);
            //var trainingLoss = CNTKLib.BinaryCrossEntropy(new Variable(classifierOutput), labels, "lossFunction");
            var trainingLoss = CNTKLib.CrossEntropyWithSoftmax(new Variable(classifierOutput), labels, "lossFunction");
            var prediction = CNTKLib.ClassificationError(new Variable(classifierOutput), labels, "classificationError");

            // prepare training data
            var minibatchSource = MinibatchSource.TextFormatMinibatchSource(ctfFile, streamConfigurations, MinibatchSource.InfinitelyRepeat, true);

            var featureStreamInfo = minibatchSource.StreamInfo(featureStreamName);
            var labelStreamInfo = minibatchSource.StreamInfo(labelsStreamName);

            // set per sample learning rate
            CNTK.TrainingParameterScheduleDouble learningRatePerSample = new CNTK.TrainingParameterScheduleDouble(0.001, 1);

            AdditionalLearningOptions opts = new AdditionalLearningOptions();
            opts.l2RegularizationWeight = 0.001;
            var ps = classifierOutput.Parameters();
            IList<Learner> parameterLearners = new List<Learner>() { Learner.SGDLearner(classifierOutput.Parameters(), learningRatePerSample, opts) };
            Trainer trainer = Trainer.CreateTrainer(classifierOutput, trainingLoss, prediction, parameterLearners);

            const uint minibatchSize = 8000;
            int outputFrequencyInMinibatches = 100, i = 0;
            int epochs = 100;
            while (epochs > 0)
            {
                var minibatchData = minibatchSource.GetNextMinibatch(minibatchSize, device);
                var arguments = new Dictionary<Variable, MinibatchData>
                {
                    { input, minibatchData[featureStreamInfo] },
                    { labels, minibatchData[labelStreamInfo] }
                };

                trainer.TrainMinibatch(arguments, device);
                TestHelper.PrintTrainingProgress(trainer, i++, outputFrequencyInMinibatches);

                // MinibatchSource is created with MinibatchSource.InfinitelyRepeat.
                // Batching will not end. Each time minibatchSource completes an sweep (epoch),
                // the last minibatch data will be marked as end of a sweep. We use this flag
                // to count number of epochs.
                if (TestHelper.MiniBatchDataIsSweepEnd(minibatchData.Values))
                {
                    epochs--;
                }
            }

            // save the trained model
            classifierOutput.Save(modelFile);

            // validate the model
            var minibatchSourceNewModel = MinibatchSource.TextFormatMinibatchSource(ctfFile, streamConfigurations, MinibatchSource.FullDataSweep);
            TestHelper.ValidateLSTMModelWithMiniBatchSource(modelFile, minibatchSourceNewModel, featureStreamName, labelsStreamName, device);
        }

        public static Function RecurrentCNN(Variable features, DeviceDescriptor device, int[] imageDim)
        {
            Function layer1 = ConvolutionLayer(features, device, 1, 16);
            //Function layer2 = ConvolutionLayer(layer1, device, 16, 16);

            // MAX POOLING 1x5
            //Function pooling1 = MaxPoolingLayer(layer2, 1, 5, 1, 2);

            //Function layer3 = ConvolutionLayer(pooling1, device, 16, 16);
            //Function layer4 = ConvolutionLayer(layer3, device, 16, 16);

            // MAX POOLING 1x4
            //Function pooling2 = MaxPoolingLayer(layer4, 1, 4, 1, 2);

            //Function layer5 = ConvolutionLayer(pooling2, device, 16, 16);
            Function layer6 = ConvolutionLayer(layer1, device, 16, 16);

            // MAX POOLING 1x2
            Function pooling3 = MaxPoolingLayer(layer6, 3, 3, 1, 1);

            // RESHAPE to 1xN -> 
            Function reshape = CNTKLib.Reshape(pooling3, new int[] { pooling3.Output.Shape.Dimensions.Aggregate((d1, d2) => d1 * d2) });

            // Bidirectional GRU
            Function gru1 = BidirectionalGRU(reshape, device);

            // Bidirectional GRU
            Function gru2 = BidirectionalGRU(gru1, device);

            // Dense
            Function dense1 = TestHelper.Dense(gru1, 64, device, Activation.ReLU);
            return TestHelper.Dense(dense1, 2, device, Activation.Sigmoid, "classifierOutput");
        }
        private static Function ConvolutionLayer(Variable features, DeviceDescriptor device, int inputChannels = 1, int outputFeatureMaps = 64)
        {
            // parameter initialization hyper parameter
            double convWScale = 0.26;
            int kernelWidth = 3;
            int kernelHeight = 3;
            var convParams = new Parameter(new int[] { kernelWidth, kernelHeight, inputChannels, outputFeatureMaps }, DataType.Float, CNTKLib.GlorotUniformInitializer(convWScale, -1, 2, 4), device);

            // horizontalStride=1, verticalStride=1
            Function convFunction = CNTKLib.Convolution(convParams, features, new int[] { 1, 1, inputChannels });

            // Apply batch normalization
            Parameter biasParams = new Parameter(new int[] { NDShape.InferredDimension }, DataType.Float, CNTKLib.GlorotUniformInitializer(CNTKLib.DefaultParamInitScale, CNTKLib.SentinelValueForInferParamInitRank, CNTKLib.SentinelValueForInferParamInitRank, 1));
            Parameter scaleParams = new Parameter(new int[] { NDShape.InferredDimension }, DataType.Float,CNTKLib.GlorotUniformInitializer(CNTKLib.DefaultParamInitScale, CNTKLib.SentinelValueForInferParamInitRank, CNTKLib.SentinelValueForInferParamInitRank, 1));
            Constant runningMean = new Constant(new int[] { NDShape.InferredDimension }, 0.0f, device);
            Constant runningInvStd = new Constant(new int[] { NDShape.InferredDimension }, 0.0f, device);
            Constant runningCount = Constant.Scalar(0.0f, device);
            Function normalizedConv = CNTKLib.BatchNormalization(convFunction, scaleParams, biasParams, runningMean, runningInvStd, runningCount, true);

            //Apply RELU Activation function
            return CNTKLib.ReLU(normalizedConv);
        }

        private static Function MaxPoolingLayer(Variable operand, int width, int height, int hStride, int vStride)
        {
            return CNTKLib.Pooling(operand, PoolingType.Max, new int[] { width, height }, new int[] { hStride, vStride }, new bool[] { true });
        }

        private static Function BidirectionalGRU(Variable input, DeviceDescriptor device)
        {
            Parameter weights = new Parameter(new int[] { NDShape.InferredDimension, NDShape.InferredDimension }, DataType.Float, CNTKLib.GlorotUniformInitializer(CNTKLib.DefaultParamInitScale, CNTKLib.SentinelValueForInferParamInitRank, CNTKLib.SentinelValueForInferParamInitRank, 1));
            Function rnn = CNTKLib.OptimizedRNNStack(input, weights, 2, 64, true, "gru");
            return CNTKLib.Tanh(rnn);
        }
    }
}
