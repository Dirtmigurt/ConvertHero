using CNTK;
using ConvertHero.AudioFileHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.CNTKModels
{
    public class ConvolutionalNeuralNetwork
    {
        /// <summary>
        /// Train and evaluate a image classifier for MNIST data.
        /// </summary>
        /// <param name="device">CPU or GPU device to run training and evaluation</param>
        /// <param name="useConvolution">option to use convolution network or to use multilayer perceptron</param>
        /// <param name="forceRetrain">whether to override an existing model.
        /// if true, any existing model will be overridden and the new one evaluated. 
        /// if false and there is an existing model, the existing model is evaluated.</param>
        public static void TrainAndEvaluate(DeviceDescriptor device, string ctfFile, bool useConvolution, bool forceRetrain)
        {
            int featureLength = 100;
            int windowSize = 25;
            var featureStreamName = "features";
            var labelsStreamName = "labels";
            var classifierName = "classifierOutput";
            Function classifierOutput;
            int[] imageDim = useConvolution ? new int[] { windowSize, featureLength, 1 } : new int[] { featureLength * windowSize };
            int imageSize = featureLength * windowSize;
            int numClasses = 2;

            IList<StreamConfiguration> streamConfigurations = new StreamConfiguration[]
                { new StreamConfiguration(featureStreamName, imageSize), new StreamConfiguration(labelsStreamName, numClasses) };

            string modelFile = useConvolution ? @"C:\test\Temp\Convolution.model" : @"C:\test\Temp\MLP.model";

            // If a model already exists and not set to force retrain, validate the model and return.
            if (File.Exists(modelFile) && !forceRetrain)
            {
                var minibatchSourceExistModel = MinibatchSource.TextFormatMinibatchSource(ctfFile, streamConfigurations, MinibatchSource.InfinitelyRepeat, true);
                TestHelper.ValidateModelWithMinibatchSource(modelFile, minibatchSourceExistModel, imageDim, numClasses, featureStreamName, labelsStreamName, classifierName, device);
                return;
            }

            // build the network
            var input = CNTKLib.InputVariable(imageDim, DataType.Float, featureStreamName);
            if (useConvolution)
            {
                classifierOutput = CreateConvolutionalNeuralNetwork(input, numClasses, device, classifierName);
            }
            else
            {
                // For MLP, we like to have the middle layer to have certain amount of states.
                int hiddenLayerDim = 200;
                classifierOutput = CreateMLPClassifier(device, numClasses, hiddenLayerDim, input, classifierName);
            }

            var labels = CNTKLib.InputVariable(new int[] { numClasses }, DataType.Float, labelsStreamName);
            var trainingLoss = CNTKLib.BinaryCrossEntropy(new Variable(classifierOutput), labels, "lossFunction");
            //var trainingLoss = CNTKLib.CrossEntropyWithSoftmax(new Variable(classifierOutput), labels, "lossFunction");
            var prediction = CNTKLib.ClassificationError(new Variable(classifierOutput), labels, "classificationError");

            // prepare training data
            var minibatchSource = MinibatchSource.TextFormatMinibatchSource(ctfFile, streamConfigurations, MinibatchSource.InfinitelyRepeat, true);

            var featureStreamInfo = minibatchSource.StreamInfo(featureStreamName);
            var labelStreamInfo = minibatchSource.StreamInfo(labelsStreamName);

            // set per sample learning rate
            CNTK.TrainingParameterScheduleDouble learningRatePerSample = new CNTK.TrainingParameterScheduleDouble(0.001, 1);
            AdditionalLearningOptions opts = new AdditionalLearningOptions();
            opts.l2RegularizationWeight = 0.001;
            IList<Learner> parameterLearners = new List<Learner>() { Learner.SGDLearner(classifierOutput.Parameters(), learningRatePerSample, opts) };
            Trainer trainer = Trainer.CreateTrainer(classifierOutput, trainingLoss, prediction, parameterLearners);

            const uint minibatchSize = 1024;
            int outputFrequencyInMinibatches = 50, i = 0;
            int epochs = 1000;
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
            var minibatchSourceNewModel = MinibatchSource.TextFormatMinibatchSource(ctfFile, streamConfigurations, MinibatchSource.InfinitelyRepeat, true);
            TestHelper.ValidateModelWithMinibatchSource(modelFile, minibatchSourceNewModel, imageDim, numClasses, featureStreamName, labelsStreamName, classifierName, device);
        }

        private static Function CreateMLPClassifier(DeviceDescriptor device, int numOutputClasses, int hiddenLayerDim,
            Function scaledInput, string classifierName)
        {
            Function dense1 = TestHelper.Dense(scaledInput, hiddenLayerDim, device, Activation.Sigmoid, "");
            Function classifierOutput = TestHelper.Dense(dense1, numOutputClasses, device, Activation.None, classifierName);
            return classifierOutput;
        }

        /// <summary>
        /// Create convolution neural network
        /// </summary>
        /// <param name="features">input feature variable</param>
        /// <param name="outDims">number of output classes</param>
        /// <param name="device">CPU or GPU device to run the model</param>
        /// <param name="classifierName">name of the classifier</param>
        /// <returns>the convolution neural network classifier</returns>
        static Function CreateConvolutionalNeuralNetwork(Variable features, int outDims, DeviceDescriptor device, string classifierName)
        {
            // 40x9x1 -> 16x5x4
            int kernelWidth1 = 5, kernelHeight1 = 5, numInputChannels1 = 1, outFeatureMapCount1 = 4;
            int hStride1 = 2, vStride1 = 2;
            int poolingWindowWidth1 = 5, poolingWindowHeight1 = 5;

            Function pooling1 = ConvolutionWithMaxPooling(features, device, kernelWidth1, kernelHeight1, numInputChannels1, outFeatureMapCount1, hStride1, vStride1, poolingWindowWidth1, poolingWindowHeight1);

            // 16x5x4 -> 6x3x8
            int kernelWidth2 = 5, kernelHeight2 = 5, numInputChannels2 = outFeatureMapCount1, outFeatureMapCount2 = 8;
            int hStride2 = 2, vStride2 = 2;
            int poolingWindowWidth2 = 5, poolingWindowHeight2 = 5;

            Function pooling2 = ConvolutionWithMaxPooling(pooling1, device, kernelWidth2, kernelHeight2, numInputChannels2, outFeatureMapCount2, hStride2, vStride2, poolingWindowWidth2, poolingWindowHeight2);

            Function outlayer = TestHelper.Dense(pooling2, outDims, device, Activation.None, classifierName);
            return outlayer;
        }

        private static Function ConvolutionWithMaxPooling(Variable features, DeviceDescriptor device,
            int kernelWidth, int kernelHeight, int numInputChannels, int outFeatureMapCount,
            int hStride, int vStride, int poolingWindowWidth, int poolingWindowHeight)
        {
            // parameter initialization hyper parameter
            var convParams = new Parameter(new int[] { kernelWidth, kernelHeight, numInputChannels, outFeatureMapCount }, DataType.Float,
                CNTKLib.GlorotUniformInitializer(CNTKLib.DefaultParamInitScale, -1, 2), device);
            Function convFunction = CNTKLib.ReLU(CNTKLib.Convolution(convParams, features, new int[] { 1, 1, numInputChannels } /* strides */));

            Function pooling = CNTKLib.Pooling(convFunction, PoolingType.Max, new int[] { poolingWindowWidth, poolingWindowHeight }, new int[] { hStride, vStride }, new bool[] { true });
            return pooling;
        }
    }
}
