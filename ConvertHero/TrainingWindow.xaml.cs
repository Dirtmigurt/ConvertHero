namespace ConvertHero
{
    using CNTK;
    using ConvertHero.AudioFileHelpers;
    using ConvertHero.CNTKModels;
    using ConvertHero.Models;
    using Melanchall.DryWetMidi.Smf;
    using Melanchall.DryWetMidi.Smf.Interaction;
    using Melanchall.DryWetMidi.Standards;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Forms;

    /// <summary>
    /// Interaction logic for TrainingWindow.xaml
    /// </summary>
    public partial class TrainingWindow : Window
    {
        private string AudioFileName { get; set; }

        private string ChartFileName { get; set; }

        public TrainingWindow()
        {
            InitializeComponent();
        }

        private void BtnOpenAudio_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Audio|*.mp3;*.ogg;*.wav";
            DialogResult result = fileDialog.ShowDialog ();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    string file = fileDialog.FileName;
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        return;
                    }

                    this.AudioFileTextBox.Text = file;
                    this.AudioFileName = file;
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }

            this.TrainModelButton.IsEnabled = !(string.IsNullOrWhiteSpace(this.ChartFileName) || string.IsNullOrWhiteSpace(this.AudioFileName));
        }

        private void BtnOpenChart_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Chart|*.chart;*.mid";
            DialogResult result = fileDialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    string file = fileDialog.FileName;
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        return;
                    }

                    this.ChartFileTextBox.Text = file;
                    this.ChartFileName = file;
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }

            this.TrainModelButton.IsEnabled = !(string.IsNullOrWhiteSpace(this.ChartFileName) || string.IsNullOrWhiteSpace(this.AudioFileName));
        }

        private void TrainModelButton_Click(object sender, RoutedEventArgs e)
        {
            //GetMidiFileLabels(@"E:\clonehero-win64\Songs\Rock Band 3 DLC\Rock Band 3 - DLC\Breaking Benjamin - Sooner or Later\notes.mid", 50);
            //BuildMasterFeaturefile();
            // Load the Audio file (features)
            //float[,] features = GetAudioFileFeatures();

            // Load the chart file (labels)
            //float[,] notesbyFrames = GetAudioFileLabels();

            //string outputFile = System.IO.Path.ChangeExtension(this.AudioFileName, ".csv");
            //outputFile = System.IO.Path.ChangeExtension(this.AudioFileName, "Mel.ctf");
            //WriteFeatureLabelFile(outputFile, features, notesbyFrames);

            string featureFile = @"C:\test\Workspace\CloneHeroOnsetFeatureSetV2.ctf";
            //CNTKModels.LSTMSequenceClassifier.ValidateModelFile(@"C:\test\LSTMOnset.model", featureFile, DeviceDescriptor.GPUDevice(0));
            CNTKModels.LSTMSequenceClassifier.Train(DeviceDescriptor.CPUDevice, featureFile, false); 
            //CNTKModels.ConvolutionalNeuralNetwork.TrainAndEvaluate(DeviceDescriptor.CPUDevice, featureFile, true, true);
            //CNTKModels.RecurrentConvolutionalNeuralNetwork.TrainAndEvaluate(DeviceDescriptor.GPUDevice(0), featureFile, true);
            //CNTKModels.LSTMSequenceClassifier.ValidateModelFile(@"C:\test\Temp\Convolution.model", featureFile, DeviceDescriptor.GPUDevice(0));
            
            ;
        }

        private void BuildMasterFeaturefile()
        {
            //using (StreamWriter multiWriter = new StreamWriter(File.OpenWrite(@"D:\Workspace\CloneHeroMultiFeatureSet.ctf")))
            using (StreamWriter onsetWriter = new StreamWriter(File.OpenWrite(@"C:\test\Workspace\CloneHeroOnsetFeatureSetV2.ctf")))
            {
                foreach (string folder in GetLeafDirectories(@"D:\clonehero-win64\Songs\Anti Hero\Anti Hero\Tier 15 - [FULL ALBUM] Foreign Obejcts - Galactic Prey (2015) (FrOoGle)\09. Direct Contact With the Dead (FrOoGle)"))
                {
                    try
                    {
                        Console.WriteLine($"{folder}");
                        // Alright featurize this shit so we can learn it
                        // Features can be found in .mid or .chart files
                        string chartFile = Directory.EnumerateFiles(folder, "*.chart").FirstOrDefault();
                        string midFile = Directory.EnumerateFiles(folder, "*.mid").FirstOrDefault();

                        List<string> audioFiles = new List<string>();
                        foreach (string mp3File in Directory.EnumerateFiles(folder, "*.mp3"))
                        {
                            audioFiles.Add(mp3File);
                        }

                        foreach (string oggFile in Directory.EnumerateFiles(folder, "*.ogg"))
                        {
                            audioFiles.Add(oggFile);
                        }

                        foreach (string wavFile in Directory.EnumerateFiles(folder, "*.wav"))
                        {
                            audioFiles.Add(wavFile);
                        }

                        int frameRate = 50;
                        // look back 1/4 of a second, and ahead 1/4 of a second
                        int window = frameRate / 4;

                        // Load the chart file (labels)
                        float[,] y = string.IsNullOrWhiteSpace(midFile) ? GetChartFileLabels(chartFile, frameRate) : GetMidiFileLabels(midFile, frameRate);
                        y = ReshapeFeatureWindow(y, 1);

                        // Load the Audio file (features)
                        float[,] x = GetAudioFileFeatures(audioFiles, frameRate, 1024, 0);

                        // Write the song to the output files
                        int featureLength = x.GetLength(1);
                        int labelLength = y.GetLength(1);
                        int lines = Math.Min(x.GetLength(0), y.GetLength(0));

                        for (int step = 0; step < lines; step++)
                        {
                            List<string> multiFeatureColumns = new List<string> { "|features" };
                            List<string> onsetFeatureColumns = new List<string> { "|features" };
                            for (int f = 0; f < featureLength; f++)
                            {
                                multiFeatureColumns.Add(x[step, f].ToString());
                                onsetFeatureColumns.Add(x[step, f].ToString());
                            }

                            multiFeatureColumns.Add("|labels");
                            onsetFeatureColumns.Add("|labels");
                            for (int l = 0; l < labelLength; l++)
                            {
                                multiFeatureColumns.Add(y[step, l].ToString());
                            }

                            //multiWriter.WriteLine(string.Join(" ", multiFeatureColumns));
                            onsetWriter.WriteLine(string.Join(" ", multiFeatureColumns));
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

        private float[,] ReshapeFeatureWindow(float[,] y, int window)
        {
            int frames = y.GetLength(0);
            int featureCount = y.GetLength(1);
            float[,] fs = new float[frames, 2];

            for (int i = 0; i < frames; i++)
            {
                // check if a note is within the window
                bool onset = false;
                for (int j = Math.Max(0, i - window); j <= Math.Min(frames - 1, i + window); j++)
                {
                    if (y[j, 0] < 0.5f)
                    {
                        onset = true;
                        break;
                    }
                }

                fs[i, 0] = onset ? 0 : 1;
                fs[i, 1] = onset ? 1 : 0;
            }

            return fs;
        }

        private IEnumerable<string> GetLeafDirectories(string root)
        {
            bool hasSubfolders = false;
            foreach(string subFolder in Directory.EnumerateDirectories(root))
            {
                hasSubfolders = true;
                foreach(string leafFolder in GetLeafDirectories(subFolder))
                {
                    yield return leafFolder;
                }
            }

            // If there were no more levels of recursion to go down, return this directory as a leaf
            if (!hasSubfolders)
            {
                yield return root;
            }
        }

        private float[,] GetAudioFileFeatures(string audioFile)
        {
            // test audio file
            int frameRate = 50;
            using (AudioFileHelpers.SampleReader reader = new AudioFileHelpers.SampleReader(audioFile, frameRate, 2048))
            {
                float[] buffer;
                float[] freqBands = new float[] { 43.06640625f, 64.599609375f, 86.1328125f, 107.666015625f, 129.19921875f, 150.732421875f, 172.265625f, 193.798828125f, 215.33203125f, 236.865234375f, 258.3984375f, 279.931640625f, 301.46484375f, 322.998046875f, 344.53125f, 366.064453125f, 387.59765625f, 409.130859375f, 430.6640625f, 452.197265625f, 473.73046875f, 495.263671875f, 516.796875f, 538.330078125f, 559.86328125f, 581.396484375f, 602.9296875f, 624.462890625f, 645.99609375f, 667.529296875f, 689.0625f, 710.595703125f, 732.12890625f, 753.662109375f, 775.1953125f, 796.728515625f, 839.794921875f, 861.328125f, 882.861328125f, 904.39453125f, 925.927734375f, 968.994140625f, 990.52734375f, 1012.060546875f, 1055.126953125f, 1076.66015625f, 1098.193359375f, 1141.259765625f, 1184.326171875f, 1205.859375f, 1248.92578125f, 1270.458984375f, 1313.525390625f, 1356.591796875f, 1399.658203125f, 1442.724609375f, 1485.791015625f, 1528.857421875f, 1571.923828125f, 1614.990234375f, 1658.056640625f, 1701.123046875f, 1765.72265625f, 1808.7890625f, 1873.388671875f, 1916.455078125f, 1981.0546875f, 2024.12109375f, 2088.720703125f, 2153.3203125f, 2217.919921875f, 2282.51953125f, 2347.119140625f, 2411.71875f, 2497.8515625f, 2562.451171875f, 2627.05078125f, 2713.18359375f, 2799.31640625f, 2885.44921875f, 2950.048828125f, 3036.181640625f, 3143.84765625f, 3229.98046875f, 3316.11328125f, 3423.779296875f, 3509.912109375f, 3617.578125f, 3725.244140625f, 3832.91015625f, 3940.576171875f, 4069.775390625f, 4177.44140625f, 4306.640625f, 4435.83984375f, 4565.0390625f, 4694.23828125f, 4844.970703125f, 4974.169921875f, 5124.90234375f, 5275.634765625f, 5426.3671875f, 5577.099609375f, 5749.365234375f, 5921.630859375f, 6093.896484375f, 6266.162109375f, 6459.9609375f, 6653.759765625f, 6847.55859375f, 7041.357421875f, 7256.689453125f, 7450.48828125f, 7687.353515625f, 7902.685546875f, 8139.55078125f, 8376.416015625f, 8613.28125f, 8871.6796875f, 9130.078125f, 9388.4765625f, 9668.408203125f, 9948.33984375f, 10249.8046875f, 10551.26953125f, 10852.734375f, 11175.732421875f, 11498.73046875f, 11843.26171875f, 12187.79296875f, 12553.857421875f, 12919.921875f, 13285.986328125f, 13673.583984375f, 14082.71484375f, 14491.845703125f, 14922.509765625f, 15353.173828125f, 15805.37109375f, 16257.568359375f };
                Windowing hannWindow = new Windowing(WindowingType.Hann);
                TriangularBands triBands = new TriangularBands(freqBands, reader.SampleRate, 0);
                SuperFlux superFlux = new SuperFlux(8, 2);
                SimpleOnsetDetectors onsets = new SimpleOnsetDetectors(reader.SampleRate);
                MelBands melBands = new MelBands(40, reader.SampleRate);
                List<float[]> melFrames = new List<float[]>();
                List<float[]> triangleBandFrames = new List<float[]>();

                // Lists to store onset detection values for each frame
                List<float> superFluxValues = new List<float>();
                List<float> hfcValues = new List<float>();
                List<float> complexValues = new List<float>();
                List<float> complexPhaseValues = new List<float>();
                List<float> fluxValues = new List<float>();
                List<float> melFluxValues = new List<float>();
                List<float> rmsValues = new List<float>();
                while (reader.Read(out buffer) > 0)
                {
                    // run buffer through Hann windowing
                    hannWindow.Compute(ref buffer);

                    // calculate the frequency magnitues of the hann window
                    (float[] mag, float[] phase) = CartesianToPolar.ConvertComplexToPolar(Spectrum.ComputeFFT(buffer, reader.SampleRate));

                    // Calculate the simple onset detection functions
                    //hfcValues.Add(onsets.ComputeHFC(mag));
                    //complexValues.Add(onsets.ComputeComplex(mag, phase));
                    //complexPhaseValues.Add(onsets.ComputeComplexPhase(mag, phase));
                    //fluxValues.Add(onsets.ComputeFlux(mag));
                    //melFluxValues.Add(onsets.ComputeMelFlux(mag));
                    //rmsValues.Add(onsets.ComputeRms(mag));
                    melFrames.Add(melBands.Compute(mag));

                    // run the spectrum through triangle filter bands
                    //float[] tribands = triBands.ComputeTriangleBands(mag);
                    //triangleBandFrames.Add(tribands);

                    // run the triangle filtered bands through superFlux to get a measure of how different this frame is from the previous frame.
                    //superFluxValues.Add(superFlux.Compute(tribands));
                }

                //NoveltyCurve ncurve = new NoveltyCurve(WeightType.Linear, frameRate, false);
                //List<float> novelty = ncurve.ComputeAll(melFrames).ToList();

                melFrames = MathHelpers.Transpose(melFrames);
                for (int band = 0; band < melFrames.Count; band++)
                {
                    SignalNormalization.MeanCenterNormalize(melFrames[band]);
                }
                melFrames = MathHelpers.Transpose(melFrames);

                int frames = melFrames.Count;
                int window = 4;
                int featureCount = melFrames.First().Length;
                float[,] fs = new float[frames, featureCount * (2 * window + 1)];

                for(int i = 0; i < melFrames.Count; i++)
                {
                    // append each mel frequency frame from i - window to i + window
                    int index = 0;
                    for(int j = i - window; j <= i + window; j++)
                    {
                        if (j < 0 || j >= melFrames.Count)
                        {
                            // add a column on all 0's
                            for(int k = 0; k < featureCount; k++)
                            {
                                fs[i, index++] = 0;
                            }
                        }
                        else
                        {
                            for(int k = 0; k < featureCount; k++)
                            {
                                fs[i, index++] = melFrames[j][k];
                            }
                        }
                    }
                }

                return fs;
            }
        }

        private float[,] GetAudioFileFeatures(List<string> audioFiles, int frameRate, int samplesPerWindow, int window = 0)
        {
            using (MultiFileSampleReader mfSampleReader = new MultiFileSampleReader(audioFiles, frameRate, samplesPerWindow))
            {
                float[] buffer;
                Windowing hannWindow = new Windowing(WindowingType.Hann);
                MelBands melBands = new MelBands(40, mfSampleReader.SampleRate, log:true);
                List<float[]> melFrames = new List<float[]>();
                while (mfSampleReader.Read(out buffer) > 0)
                {
                    // run buffer through Hann windowing
                    hannWindow.Compute(ref buffer);

                    // calculate the frequency magnitues of the hann window
                    //(float[] mag, float[] phase) = CartesianToPolar.ConvertComplexToPolar(Spectrum.ComputeFFT(buffer, mfSampleReader.SampleRate));
                    float[] decibels = MathHelpers.ComputeBinMagnitudes(Spectrum.ComputeFFT(buffer, mfSampleReader.SampleRate), maxBins: 100);
                    melFrames.Add(decibels);
                    // Calculate the simple onset detection functions
                    //hfcValues.Add(onsets.ComputeHFC(mag));
                    //complexValues.Add(onsets.ComputeComplex(mag, phase));
                    //complexPhaseValues.Add(onsets.ComputeComplexPhase(mag, phase));
                    //fluxValues.Add(onsets.ComputeFlux(mag));
                    //melFluxValues.Add(onsets.ComputeMelFlux(mag));
                    //rmsValues.Add(onsets.ComputeRms(mag));
                    //melFrames.Add(melBands.Compute(mag));

                    // run the spectrum through triangle filter bands
                    //float[] tribands = triBands.ComputeTriangleBands(mag);
                    //triangleBandFrames.Add(tribands);

                    // run the triangle filtered bands through superFlux to get a measure of how different this frame is from the previous frame.
                    //superFluxValues.Add(superFlux.Compute(tribands));
                }

                // compute mean and stdev of melFrames


                //NoveltyCurve ncurve = new NoveltyCurve(WeightType.Linear, frameRate, false);
                //List<float> novelty = ncurve.ComputeAll(melFrames).ToList();

                //melFrames = MathHelpers.Transpose(melFrames);
                //for (int band = 0; band < melFrames.Count; band++)
                //{
                //    SignalNormalization.MeanCenterNormalize(melFrames[band]);
                //}
                //melFrames = MathHelpers.Transpose(melFrames);

                int frames = melFrames.Count;
                int featureCount = melFrames.First().Length;
                float[,] fs = new float[frames, featureCount * (2 * window + 1)];

                for (int i = 0; i < melFrames.Count; i++)
                {
                    // append each mel frequency frame from i - window to i + window
                    int index = 0;
                    for (int j = i - window; j <= i + window; j++)
                    {
                        if (j < 0 || j >= melFrames.Count)
                        {
                            // add a column on all 0's
                            for (int k = 0; k < featureCount; k++)
                            {
                                fs[i, index++] = 0;
                            }
                        }
                        else
                        {
                            for (int k = 0; k < featureCount; k++)
                            {
                                fs[i, index++] = melFrames[j][k];
                            }
                        }
                    }
                }

                return fs;
            }
        }

        private float[,] GetAudioFileLabels()
        {
            return AudioFileHelpers.ChartFileReader.GetChartFrames(this.ChartFileName, 50);
        }

        private float[,] GetChartFileLabels(string chartFile, int frameRate)
        {
            return AudioFileHelpers.ChartFileReader.GetChartFrames(chartFile, frameRate);
        }

        private float[,] GetMidiFileLabels(string midFile, int frameRate)
        {
            MidiFile midiFile = MidiFile.Read(midFile);
            TicksPerQuarterNoteTimeDivision x = midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision;
            int chartResolution = x.TicksPerQuarterNote;
            var syncEvents = LoadSyncEvents(midiFile);
            var noteTracks = LoadNoteEvents(midiFile, syncEvents, chartResolution);
            NoteTrack track = noteTracks.Where(t => t.Title.Equals("PART GUITAR", StringComparison.InvariantCultureIgnoreCase)).First();
            List<Event> eventList = new List<Event>(syncEvents);
            foreach(ChartEvent ev in track.Notes)
            {
                if(ev.Type >= 96 && ev.Type <= 100)
                {
                    eventList.Add(new ChartEvent(ev.Tick, ev.Type - 96, ev.Sustain));
                }
            }

            // Build a list of note events with absolute time/ not ticks
            double tickDelta = 0;//60.0 / (this.chartResolution * beatsPerMinute);
            double timePointer = 0;
            long previousTick = 0;
            int maxFrame = 0;
            foreach (Event chartEvent in eventList.OrderBy(e => e.Tick))
            {
                double elapsed = (chartEvent.Tick - previousTick) * tickDelta;
                timePointer += elapsed;
                chartEvent.AbsoluteTime = timePointer;
                previousTick = chartEvent.Tick;
                chartEvent.AbsoluteTime = timePointer;
                if (chartEvent is SyncEvent)
                {
                    SyncEvent syncEvent = chartEvent as SyncEvent;
                    // add to the elapsed, update the bpm/tickDelta
                    if(syncEvent.BeatsPerMinute > 0)
                    {
                        tickDelta = 60.0 / (chartResolution * syncEvent.BeatsPerMinute);
                    }
                }
                else
                {
                    ChartEvent ev = chartEvent as ChartEvent;
                    // Assumes that the sustain does not span a BPM change... probably good enough
                    ev.SustainSeconds = ev.Sustain * tickDelta;
                    maxFrame = (int)(timePointer / (1.0 / frameRate));
                }
            }

            float[,] result = new float[maxFrame + 1, 7];
            // Set all frames as a non-note until they are written to something else
            for (int i = 0; i <= maxFrame; i++)
            {
                result[i, 0] = 1;
            }

            foreach (Event chartEvent in eventList.OrderBy(e => e.Tick))
            {
                if (chartEvent is ChartEvent)
                {
                    ChartEvent ev = chartEvent as ChartEvent;

                    // Shift all Green-Orange notes up by one, shift Open notes down to 6
                    int note = ev.Type;
                    int startFrame = (int)(chartEvent.AbsoluteTime / (1.0 / frameRate));
                    int endFrame = startFrame; //startFrame + (int)(chartEvent.AbsoluteTime + ev.SustainSeconds / (1.0 / frameRate));
                    while (startFrame <= endFrame && startFrame <= maxFrame)
                    {
                        // zero out the label frame
                        for (int i = 0; i < 7; i++)
                        {
                            result[startFrame, i] = 0;
                        }

                        for (int i = 0; i < 8; i++)
                        {
                            if ((note & (1 << i)) > 0)
                            {
                                result[startFrame, Math.Min(i + 1, 6)] = 1;
                            }
                        }

                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Load the actual tones played by each instrument in the midi file.
        /// </summary>
        /// <param name="midiFile">
        /// The midi file containing the note events.
        /// </param>
        /// <returns>
        /// Each of the instrument channels int the midi file.
        /// </returns>
        public static List<NoteTrack> LoadNoteEvents(MidiFile midiFile, List<SyncEvent> syncTrack, int chartResolution)
        {
            List<NoteTrack> tracks = new List<NoteTrack>();
            List<TrackChunk> trackChunks = midiFile.GetTrackChunks().ToList();
            foreach (TrackChunk channel in trackChunks)
            {
                string trackName = GetChannelTitle(channel);
                GeneralMidiProgram instrument = GetChannelInstrument(channel);
                List<ChartEvent> channelTrack = new List<ChartEvent>();
                int channel_index = -1;
                foreach (Note note in channel.GetNotes())
                {
                    channelTrack.Add(new ChartEvent(note.Time, note.NoteNumber, note.Length));
                    channel_index = note.Channel;
                }

                if (channelTrack.Count > 0)
                {
                    NoteTrack track = new NoteTrack(channelTrack, new List<SyncEvent>(syncTrack), chartResolution, instrument, null, trackName);
                    tracks.Add(track);
                }
            }

            return tracks;
        }

        /// <summary>
        /// Load the tempo and time signature changes from the midi file.
        /// </summary>
        /// <param name="midiFile">
        /// The midi file containing the sync events.
        /// </param>
        /// <returns>
        /// A list of all time signatures and tempo changes.
        /// </returns>
        public static List<SyncEvent> LoadSyncEvents(MidiFile midiFile)
        {
            List<SyncEvent> syncTrack = new List<SyncEvent>();
            foreach (var tempoEvent in midiFile.GetTempoMap().Tempo.AsEnumerable())
            {
                syncTrack.Add(new SyncEvent(tempoEvent.Time, tempoEvent.Value.BeatsPerMinute));
            }

            foreach (var timeSignatureEvent in midiFile.GetTempoMap().TimeSignature.AsEnumerable())
            {
                syncTrack.Add(new SyncEvent(timeSignatureEvent.Time, timeSignatureEvent.Value.Numerator, timeSignatureEvent.Value.Denominator));
            }

            List<ITimedObject> x = midiFile.GetTimedEventsAndNotes().ToList();
            return syncTrack.OrderBy(k => k.Tick).ToList();
        }

        /// <summary>
        /// Reads the instrument that is playing the tones in the specified channel.
        /// </summary>
        /// <param name="channel">
        /// The midi channel for a single instrument.
        /// </param>
        /// <returns>
        /// The Midi instrument code for the channel.
        /// </returns>
        private static GeneralMidiProgram GetChannelInstrument(TrackChunk channel)
        {
            TimedEvent programChangeEvent = channel.GetTimedEvents().Where(e => e.Event is ProgramChangeEvent).FirstOrDefault();
            if (programChangeEvent == null)
            {
                return GeneralMidiProgram.DistortionGuitar;
            }

            ProgramChangeEvent ev = programChangeEvent.Event as ProgramChangeEvent;
            return (GeneralMidiProgram)(int)ev.ProgramNumber;
        }

        /// <summary>
        /// Reads the custom title of the midi channel.
        /// </summary>
        /// <param name="channel">
        /// The midi channel for a single instrument.
        /// </param>
        /// <returns>
        /// The custom title of the midi channel.
        /// </returns>
        private static string GetChannelTitle(TrackChunk channel)
        {
            // Get Channel Names
            TimedEvent trackNameEvent = channel.GetTimedEvents().Where(e => e.Event is SequenceTrackNameEvent).FirstOrDefault();
            if (trackNameEvent == null)
            {
                return $"Untitled";
            }

            SequenceTrackNameEvent ev = trackNameEvent.Event as SequenceTrackNameEvent;
            return ev.Text;
        }

        private void WriteFeatureLabelCsvFile(string outFile, float[,] x, float[,] y)
        {
            int lines = x.GetLength(0);
            int featureLength = x.GetLength(1);
            int labelLength = y.GetLength(1);
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }

            using (StreamWriter writer = new StreamWriter(File.OpenWrite(outFile)))
            {
                writer.WriteLine("superflux,hfc,complex,complexphase,flux,melflux,rms,novelty,super,label");
                for (int i = 0; i < lines; i++)
                {
                    // Write the feature vector
                    List<string> columns = new List<string>();
                    //columns.Add("|features");
                    for (int f = 0; f < featureLength; f++)
                    {
                        columns.Add(x[i,f].ToString());
                    }

                    //columns.Add("|labels");
                    columns.Add(y[i, 0] > 0.5 ? "0" : "1");

                    //for (int l = 0; l < labelLength; l++)
                    //{
                    //    columns.Add(y[i, l].ToString());
                    //}

                    writer.WriteLine(string.Join(",", columns));
                }
            }
        }

        private void WriteWindowedFeatureLabelFile(string outFile, float[,] x, float[,] y)
        {
            int lines = x.GetLength(0);
            int featureLength = x.GetLength(1);
            int labelLength = y.GetLength(1);
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }

            // Look N steps back + N steps forward
            int window = 2;

            // concat 2 * window + 1 feature vectors together
            using (StreamWriter writer = new StreamWriter(File.OpenWrite(outFile)))
            {

                for (int step = 0; step < lines; step++)
                {
                    List<string> featureColumns = new List<string> { "|features" };
                    for(int index = step - window; index <= step + window; index++)
                    {
                        if(index < 0 || index >= lines)
                        {
                            // Woops add an empty feature row
                            for(int j = 0; j < featureLength; j++)
                            {
                                featureColumns.Add("0");
                            }
                        }
                        else
                        {
                            // add a feature row
                            for(int j = 0; j < featureLength; j++)
                            {
                                featureColumns.Add(x[index, j].ToString());
                            }
                        }
                    }

                    featureColumns.Add("|labels");
                    for (int index = step - window; index <= step + window; index++)
                    {
                        if (index < 0 || index >= lines)
                        {
                            // Woops add an empty label row
                            featureColumns.Add("0");
                        }
                        else
                        {
                            // add a label row
                            featureColumns.Add(y[index, 0] > 0.5 ? "0" : "1");
                        }
                    }

                    writer.WriteLine(string.Join(" ", featureColumns));
                }
            }
        }

        private void WriteFeatureLabelFile(string outFile, float[,] x, float[,] y)
        {
            int featureLength = x.GetLength(1);
            int labelLength = y.GetLength(1);
            int lines = Math.Min(x.GetLength(0), y.GetLength(0));

            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }

            // concat 2 * window + 1 feature vectors together
            using (StreamWriter writer = new StreamWriter(File.OpenWrite(outFile)))
            {
                for (int step = 0; step < lines; step++)
                {
                    List<string> featureColumns = new List<string> { "|features" };
                    for(int f = 0; f < featureLength; f++)
                    {
                        featureColumns.Add(x[step, f].ToString());
                    }

                    featureColumns.Add("|labels");
                    for(int l = 0; l < labelLength; l++)
                    {
                        featureColumns.Add(y[step, l].ToString());
                    }

                    writer.WriteLine(string.Join(" ", featureColumns));
                }
            }
        }
    }
}
