using CNTK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.CNTKModels
{
    public enum Activation
    {
        None,
        ReLU,
        Sigmoid,
        Tanh
    }

    public class TestHelper
    {
        public static Function Dense(Variable input, int outputDim, DeviceDescriptor device, Activation activation = Activation.None, string outputName = "")
        {
            if (input.Shape.Rank != 1)
            {
                int newDim = input.Shape.Dimensions.Aggregate((d1, d2) => d1 * d2);
                input = CNTKLib.Reshape(input, new int[] { newDim });
            }

            Function fullyConnected = FullyConnectedLinearLayer(input, outputDim, device);
            switch (activation)
            {
                default:
                case Activation.None:
                    return fullyConnected;
                case Activation.ReLU:
                    return CNTKLib.ReLU(fullyConnected, outputName);
                case Activation.Sigmoid:
                    return CNTKLib.Sigmoid(fullyConnected, outputName);
                case Activation.Tanh:
                    return CNTKLib.Tanh(fullyConnected, outputName);
            }
        }

        public static Function FullyConnectedLinearLayer(Variable input, int outputDim, DeviceDescriptor device, string outputName = "")
        {
            int inputDim = input.Shape[0];

            int[] s = { outputDim, inputDim };
            var weights = new Parameter((NDShape)s, DataType.Float, 
                CNTKLib.GlorotUniformInitializer(
                    CNTKLib.DefaultParamInitScale,
                    CNTKLib.SentinelValueForInferParamInitRank,
                    CNTKLib.SentinelValueForInferParamInitRank, 1),
                device);
            var timesFunction = CNTKLib.Times(weights, input);

            int[] s2 = { outputDim };
            var bias = new Parameter(s2, 0.0f, device, "bias");
            return CNTKLib.Plus(bias, timesFunction, outputName);
        }

        public static void PrintTrainingProgress(Trainer trainer, int minibatchIdx, int outputFrequencyInMinibatches)
        {
            if ((minibatchIdx % outputFrequencyInMinibatches) == 0 && trainer.PreviousMinibatchSampleCount() != 0)
            {
                float trainLossValue = (float)trainer.PreviousMinibatchLossAverage();
                float evaluationValue = (float)trainer.PreviousMinibatchEvaluationAverage();
                Console.WriteLine($"Minibatch: {minibatchIdx} CrossEntropyLoss = {trainLossValue}, ErrorPercentage = {evaluationValue}");
            }
        }

        public static bool MiniBatchDataIsSweepEnd(ICollection<MinibatchData> minibatchValues)
        {
            return minibatchValues.Any(a => a.sweepEnd);
        }

        public static void PrintOutputDims(Function function, string functionName)
        {
            NDShape shape = function.Output.Shape;

            if (shape.Rank == 3)
            {
                Console.WriteLine($"{functionName} dim0: {shape[0]}, dim1: {shape[1]}, dim2: {shape[2]}");
            }
            else
            {
                Console.WriteLine($"{functionName} dim0: {shape[0]}");
            }
        }

        public static void SaveAndReloadModel(ref Function function, IList<Variable> variables, DeviceDescriptor device, uint rank = 0)
        {
            string tempModelPath = "feedForward.net" + rank;
            File.Delete(tempModelPath);

            IDictionary<string, Variable> inputVarUids = new Dictionary<string, Variable>();
            IDictionary<string, Variable> outputVarNames = new Dictionary<string, Variable>();

            foreach (var variable in variables)
            {
                if (variable.IsOutput)
                    outputVarNames.Add(variable.Owner.Name, variable);
                else
                    inputVarUids.Add(variable.Uid, variable);
            }

            function.Save(tempModelPath);
            function = Function.Load(tempModelPath, device);

            File.Delete(tempModelPath);

            var inputs = function.Inputs;
            foreach (var inputVarInfo in inputVarUids.ToList())
            {
                var newInputVar = inputs.First(v => v.Uid == inputVarInfo.Key);
                inputVarUids[inputVarInfo.Key] = newInputVar;
            }

            var outputs = function.Outputs;
            foreach (var outputVarInfo in outputVarNames.ToList())
            {
                var newOutputVar = outputs.First(v => v.Owner.Name == outputVarInfo.Key);
                outputVarNames[outputVarInfo.Key] = newOutputVar;
            }
        }

        public static float ValidateLSTMModelWithMiniBatchSource(string modelFile, MinibatchSource testMinibatchSource, string featureInputName, string labelInputName, DeviceDescriptor device, int maxCount = 100000)
        {
            Function model = Function.Load(modelFile, device);

            var featureStreamInfo = testMinibatchSource.StreamInfo(featureInputName);
            var labelStreamInfo = testMinibatchSource.StreamInfo(labelInputName);
            var modelInput = model.Arguments.First();
            var modelOutput = model.Output;

            int batchSize = 50;
            int miscountTotal = 0, totalCount = 0;
            int missTotal = 0, fpTotal = 0;
            int predictedOnsetTotal = 0, actualOnsetTotal = 0;
            while (true)
            {
                var minibatchData = testMinibatchSource.GetNextMinibatch((uint)batchSize, device);
                totalCount += (int)minibatchData[featureStreamInfo].numberOfSamples;

                // expected labels are in the minibatch data.
                List<int> expectedLabels = minibatchData[labelStreamInfo].data.GetDenseData<float>(modelOutput).Select(lst => lst.IndexOf(lst.Max())).ToList();
                if (expectedLabels.Contains(1))
                {
                    ;
                }

                var inputDataMap = new Dictionary<Variable, Value>() {
                    { modelInput, minibatchData[featureStreamInfo].data }
                };

                var outputDataMap = new Dictionary<Variable, Value>() {
                    { modelOutput, null }
                };

                model.Evaluate(inputDataMap, outputDataMap, device);

                List<int> predictedLabels = new List<int>();
                foreach(var lst in outputDataMap[modelOutput].GetDenseData<float>(modelOutput))
                {
                    predictedLabels.Add(lst.IndexOf(lst.Max()));
                }

                if (predictedLabels.Contains(1))
                {
                    ;
                }

                int onsetMisses = expectedLabels.Zip(predictedLabels, (a, b) => a > b ? 1 : 0).Sum();
                int onsetFP     = expectedLabels.Zip(predictedLabels, (a, b) => b > a ? 1 : 0).Sum();
                missTotal += onsetMisses;
                fpTotal += onsetFP;

                predictedOnsetTotal += predictedLabels.Sum();
                actualOnsetTotal += expectedLabels.Sum();

                Console.WriteLine($"Validating Model: Total Onsets: {actualOnsetTotal},  Predicted Onsets: {predictedOnsetTotal},  Onset Misses: {missTotal},  Onset False Positives: {fpTotal}");

                if (totalCount > maxCount)
                    break;
            }

            float errorRate = 1.0F * miscountTotal / totalCount;
            Console.WriteLine($"Model Validation Error = {errorRate}");
            return errorRate;
        }

        public static float ValidateModelWithMinibatchSource(
            string modelFile, 
            MinibatchSource testMinibatchSource,
            int[] imageDim, 
            int numClasses, 
            string featureInputName, 
            string labelInputName, 
            string outputName,
            DeviceDescriptor device, 
            int maxCount = 1000000)
        {
            Function model = Function.Load(modelFile, device);
            var imageInput = model.Arguments[0];
            var labelOutput = model.Outputs.Single(o => o.Name == outputName);

            var featureStreamInfo = testMinibatchSource.StreamInfo(featureInputName);
            var labelStreamInfo = testMinibatchSource.StreamInfo(labelInputName);

            int batchSize = 50;
            int miscountTotal = 0, totalCount = 0;
            while (true)
            {
                var minibatchData = testMinibatchSource.GetNextMinibatch((uint)batchSize, device);
                if (minibatchData == null || minibatchData.Count == 0)
                    break;
                totalCount += (int)minibatchData[featureStreamInfo].numberOfSamples;

                // expected labels are in the minibatch data.
                var labelData = minibatchData[labelStreamInfo].data.GetDenseData<float>(labelOutput);
                var expectedLabels = labelData.Select(l => l.IndexOf(l.Max())).ToList();

                var inputDataMap = new Dictionary<Variable, Value>() {
                    { imageInput, minibatchData[featureStreamInfo].data }
                };

                var outputDataMap = new Dictionary<Variable, Value>() {
                    { labelOutput, null }
                };

                model.Evaluate(inputDataMap, outputDataMap, device);
                var outputData = outputDataMap[labelOutput].GetDenseData<float>(labelOutput);
                var actualLabels = outputData.Select(l => l.IndexOf(l.Max())).ToList();
                if (actualLabels.Contains(1) || expectedLabels.Contains(1))
                {
                    ;
                }

                int misMatches = actualLabels.Zip(expectedLabels, (a, b) => a.Equals(b) ? 0 : 1).Sum();

                miscountTotal += misMatches;
                Console.WriteLine($"Validating Model: Total Samples = {totalCount}, Misclassify Count = {miscountTotal}");

                if (totalCount > maxCount)
                    break;
            }

            float errorRate = 1.0F * miscountTotal / totalCount;
            Console.WriteLine($"Model Validation Error = {errorRate}");
            return errorRate;
        }
    }
}
