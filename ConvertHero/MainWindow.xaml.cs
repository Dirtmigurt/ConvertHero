﻿namespace ConvertHero
{
    using ConvertHero.Models;
    using Melanchall.DryWetMidi.Smf;
    using Melanchall.DryWetMidi.Smf.Interaction;
    using Melanchall.DryWetMidi.Standards;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Xml.Linq;
    using System.Windows;

    using System.Windows.Forms;
    using System;
    using System.IO;
    using Melanchall.DryWetMidi.Devices;
    using ConvertHero.AudioFileHelpers;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Windows.Media;
    using System.ComponentModel;
    using MathNet.Numerics;
    using Accord.Collections;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        /// <summary>
        /// The list of time signatures and tempo events in the midi file.
        /// </summary>
        private static List<SyncEvent> SyncTrack = null;

        /// <summary>
        /// The number of ticks per quarter note that the midi file uses.
        /// </summary>
        private static int ChartResolution = -1;

        /// <summary>
        /// The file name of the Midi File.
        /// </summary>
        private static string MidiFileName;

        /// <summary>
        /// The file name of the Audio File.
        /// </summary>
        private static string AudioFileName;

        /// <summary>
        /// Gets or sets a task that is responsible for running the audio conversion work.
        /// </summary>
        private Task ConvertAudioTask { get; set; }

        /// <summary>
        /// MainWindow Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Callback function for when the OpenMidi button is clicked.
        /// This function opens a file picker dialog so the user can select a midi file.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The event args.
        /// </param>
        private void BtnOpenMidi_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Filter = "Midi|*.mid"
            };

            DialogResult result = fileDialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    string file = fileDialog.FileName;
                    if (file.EndsWith(".mid", StringComparison.InvariantCultureIgnoreCase))
                    {
                        try
                        {
                            LoadMidiFile(file);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(ex.Message, "Load Midi Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                        
                    this.FilenameTextBox.Text = file;
                    MidiFileName = file;
                    startConvertButton.Content = "Convert";
                    startConvertButton.IsEnabled = true;
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }
        }

        /// <summary>
        /// Once a midi file is selected this fuction reads all of the interesting bits,
        /// like the channels and tempo and time signatures.
        /// </summary>
        /// <param name="fileName">
        /// Name of the midi file.
        /// </param>
        private void LoadMidiFile(string fileName)
        {
            MidiFile midiFile = MidiFile.Read(fileName);
            TicksPerQuarterNoteTimeDivision x = midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision;
            ChartResolution = x.TicksPerQuarterNote;
            SyncTrack = LoadSyncEvents(midiFile);
            ObservableCollection<NoteTrack> trackList = LoadNoteEvents(midiFile);

            // Lead and bass tracks support all possible note types
            LeadTrackListView.ItemsSource = trackList;
            BassTrackListView.ItemsSource = new ObservableCollection<NoteTrack>(trackList);

            // Drum track notes must map to dictionary
            DrumTrackListView.ItemsSource = new ObservableCollection<NoteTrack>(trackList);
            return;
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
                double bpm = 60000000.0 / tempoEvent.Value.MicrosecondsPerQuarterNote;
                syncTrack.Add(new SyncEvent(tempoEvent.Time, bpm));
            }

            foreach (var timeSignatureEvent in midiFile.GetTempoMap().TimeSignature.AsEnumerable())
            {
                syncTrack.Add(new SyncEvent(timeSignatureEvent.Time, timeSignatureEvent.Value.Numerator, timeSignatureEvent.Value.Denominator));
            }

            return syncTrack.OrderBy(k => k.Tick).ToList();
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
        public static ObservableCollection<NoteTrack> LoadNoteEvents(MidiFile midiFile)
        {
            ObservableCollection<NoteTrack> tracks = new ObservableCollection<NoteTrack>();
            OutputDevice outputDevice = OutputDevice.GetAll().First();
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
                    NoteTrack track = new NoteTrack(channelTrack, new List<SyncEvent>(SyncTrack), ChartResolution, instrument, channel.GetPlayback(midiFile.GetTempoMap(), outputDevice), trackName);
                    tracks.Add(track);
                }
            }

            return tracks;
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

        /// <summary>
        /// Code to move the selected item(s) in a ListView UP relative to the non-selected items.
        /// </summary>
        /// <param name="view">
        /// The list view that should move its selected items.
        /// </param>
        /// <returns>
        /// The newly re-ordered collection of objects to display in the list view.
        /// </returns>
        private static ObservableCollection<object> MoveSelectionUp(System.Windows.Controls.ListView view)
        {
            // Null padding Preserves ordering if the first item in the list is selected and gets swapped
            ObservableCollection<object> temp = new ObservableCollection<object> { null };
            HashSet<object> selected = new HashSet<object>();
            foreach (object s in view.SelectedItems)
            {
                selected.Add(s);
            }

            foreach (object item in view.ItemsSource)
            {
                if (selected.Contains(item))
                {
                    temp.Add(item);
                    temp.Move(temp.Count - 2, temp.Count - 1);

                }
                else
                {
                    temp.Add(item);
                }
            }

            temp.Remove(null);
            return temp;
        }

        /// <summary>
        /// Code to move the selected item(s) in a ListView DOWN relative to the non-selected items.
        /// </summary>
        /// <param name="view">
        /// The list view that should move its selected items.
        /// </param>
        /// <returns>
        /// The newly re-ordered collection of objects to display in the list view.
        /// </returns>
        private static ObservableCollection<object> MoveSelectionDown(System.Windows.Controls.ListView view)
        {
            // Null padding Preserves ordering if the first item in the list is selected and gets swapped
            ObservableCollection<object> temp = new ObservableCollection<object>();
            HashSet<object> selected = new HashSet<object>();
            foreach (object s in view.SelectedItems)
            {
                selected.Add(s);
            }

            foreach (object item in view.ItemsSource)
            {
                temp.Add(item);
            }
            temp.Add(null);

            ObservableCollection<object> final = new ObservableCollection<object>();
            for (int i = temp.Count - 1; i >= 0; i--)
            {
                if (selected.Contains(temp[i]))
                {
                    final.Insert(0, temp[i]);
                    final.Move(0, 1);

                }
                else
                {
                    final.Insert(0, temp[i]);
                }
            }

            final.Remove(null);
            return final;
        }

        /// <summary>
        /// Code to remove the selected item(s) in a ListView.
        /// </summary>
        /// <param name="view">
        /// The list view that should remove its selected items.
        /// </param>
        /// <returns>
        /// The newly re-ordered collection of objects to display in the list view.
        /// </returns>
        private static ObservableCollection<object> RemoveSelection(System.Windows.Controls.ListView view)
        {
            // Null padding Preserves ordering if the first item in the list is selected and gets swapped
            ObservableCollection<object> temp = new ObservableCollection<object>();
            HashSet<object> selected = new HashSet<object>();
            foreach (object s in view.SelectedItems)
            {
                selected.Add(s);
            }

            foreach (object item in view.ItemsSource)
            {
                if (!selected.Contains(item))
                {
                    temp.Add(item);
                }
            }

            return temp;
        }

        /// <summary>
        /// Move selected items up in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeadButtonUP_Click(object sender, RoutedEventArgs e)
        {
            LeadTrackListView.ItemsSource = MoveSelectionUp(LeadTrackListView);
        }

        /// <summary>
        /// Move selected items down in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeadButtonDOWN_Click(object sender, RoutedEventArgs e)
        {
            LeadTrackListView.ItemsSource = MoveSelectionDown(LeadTrackListView);
        }

        /// <summary>
        /// Remove selected items in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeadButtonRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            LeadTrackListView.ItemsSource = RemoveSelection(LeadTrackListView);
        }

        /// <summary>
        /// Move selected items up in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BassButtonUP_Click(object sender, RoutedEventArgs e)
        {
            BassTrackListView.ItemsSource = MoveSelectionUp(BassTrackListView);
        }

        /// <summary>
        /// Move selected items down in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BassButtonDOWN_Click(object sender, RoutedEventArgs e)
        {
            BassTrackListView.ItemsSource = MoveSelectionDown(BassTrackListView);
        }

        /// <summary>
        /// Remove selected items in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BassButtonRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            BassTrackListView.ItemsSource = RemoveSelection(BassTrackListView);
        }

        /// <summary>
        /// Move selected items up in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrumButtonUP_Click(object sender, RoutedEventArgs e)
        {
            DrumTrackListView.ItemsSource = MoveSelectionUp(DrumTrackListView);
        }

        /// <summary>
        /// Move selected items down in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrumButtonDOWN_Click(object sender, RoutedEventArgs e)
        {
            DrumTrackListView.ItemsSource = MoveSelectionDown(DrumTrackListView);
        }

        /// <summary>
        /// Remove selected items in the list view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrumButtonRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            DrumTrackListView.ItemsSource = RemoveSelection(DrumTrackListView);
        }

        /// <summary>
        /// Callback function for the startConvertButton.
        /// This method merges all of the remaining note tracks for each instrument and then
        /// performs a reshaping of the notes so they fit onto a clone hero highway.
        /// It then writes these re-shaped notes out to a .chart file so that Moonscraper can read it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartConvertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.startConvertButton.IsEnabled = false;
                using (StreamWriter writer = new StreamWriter(Path.ChangeExtension(MidiFileName, ".chart")))
                {
                    // Write SONG section
                    writer.WriteLine(string.Format(Properties.Resources.SongSection, ChartResolution, Path.GetFileName(MidiFileName)));

                    // Write SYNC section
                    writer.WriteLine(string.Format(Properties.Resources.SyncTrack, string.Join("\n", SyncTrack.Select(s => s.ToString()))));

                    // Write EVENTS section
                    List<string> eventStrings = SyncTrack.Where(s => s.BeatsPerMinute > 0).Select(s => s.ToEventString()).ToList();
                    writer.WriteLine(string.Format(Properties.Resources.EventsTrack, string.Join("\n", eventStrings)));

                    // LEAD TRACKS
                    List<NoteTrack> leadTracks = new List<NoteTrack>();
                    foreach (object obj in LeadTrackListView.ItemsSource)
                    {
                        leadTracks.Add(obj as NoteTrack);
                    }

                    if (leadTracks.Count > 0)
                    {
                        NoteTrack lead = MergeManyTracks(leadTracks, CloneHeroInstrument.Single);
                        lead.CloneHeroInstrument = CloneHeroInstrument.Single.ToString();

                        // Write Note section
                        writer.WriteLine(string.Format(Properties.Resources.NoteTrack, $"{CloneHeroDifficulty.Expert}{CloneHeroInstrument.Single}", string.Join("\n", lead.Notes.Select(n => n.ToString()))));
                    }

                    // BASS TRACKS
                    List<NoteTrack> bassTracks = new List<NoteTrack>();
                    foreach (object obj in BassTrackListView.ItemsSource)
                    {
                        bassTracks.Add(obj as NoteTrack);
                    }

                    if (bassTracks.Count > 0)
                    {
                        NoteTrack bass = MergeManyTracks(bassTracks, CloneHeroInstrument.DoubleBass);
                        bass.CloneHeroInstrument = CloneHeroInstrument.DoubleBass.ToString();

                        // Write Note section
                        writer.WriteLine(string.Format(Properties.Resources.NoteTrack, $"{CloneHeroDifficulty.Expert}{CloneHeroInstrument.DoubleBass}", string.Join("\n", bass.Notes.Select(n => n.ToString()))));
                    }

                    // DRUM TRACKS
                    List<NoteTrack> drumTracks = new List<NoteTrack>();
                    foreach (object obj in DrumTrackListView.ItemsSource)
                    {
                        drumTracks.Add(obj as NoteTrack);
                    }

                    if (drumTracks.Count > 0)
                    {
                        NoteTrack drum = MergeManyTracks(drumTracks, CloneHeroInstrument.Drums);
                        drum.CloneHeroInstrument = CloneHeroInstrument.Drums.ToString();

                        // Write Note section
                        writer.WriteLine(string.Format(Properties.Resources.NoteTrack, $"{CloneHeroDifficulty.Expert}{CloneHeroInstrument.Drums}", string.Join("\n", drum.Notes.Select(n => n.ToString()))));
                    }
                }

                this.startConvertButton.Content = "Conversion Complete.";
            }
            catch(Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Conversion Failed.", MessageBoxButton.OK, MessageBoxImage.Error);
                this.startConvertButton.Content = "Conversion Failed.";
            }
        }

        /// <summary>
        /// Merge n tracks into a single track where the first track in the list overwrites all tracks
        /// that follow it.
        /// 
        /// If the track is for Guitar/Bass use the GuitarReshape() method.
        /// If the track is for Drums use the DrumReshape() method.
        /// </summary>
        /// <param name="tracks">
        /// The list of tracks to merge.
        /// </param>
        /// <param name="isDrum">
        /// Is this a drum track?
        /// </param>
        /// <returns>
        /// The single output track.
        /// </returns>
        private NoteTrack MergeManyTracks(List<NoteTrack> tracks, CloneHeroInstrument instrument)
        {
            if (tracks == null || tracks.Count == 0)
            {
                return null;
            }

            NoteTrack merged = null;
            if (tracks.Count == 1)
            {
                merged = tracks.First();
            }
            else
            {
                // Merge tracks
                merged = tracks.First();
                for (int i = 1; i < tracks.Count; i++)
                {
                    merged = MergeTwoTracks(merged, tracks[i]);
                }
            }

            // Map notes onto guitar hero highway system depending on the type of instrument it is (guitar/bass/drums).
            switch(instrument)
            {
                case CloneHeroInstrument.Single:
                    merged.GuitarReshape();
                    break;
                case CloneHeroInstrument.DoubleBass:
                    merged.BassGuitarReshape();
                    break;
                case CloneHeroInstrument.Drums:
                    merged.DrumReshape();
                    break;
                default:
                    merged.GuitarReshape();
                    break;
            }

            return merged;
        }

        /// <summary>
        /// Merge two tracks into a single track where the notes in primary overwrite the notes in secondary.
        /// </summary>
        /// <param name="primary">All notes in this track will be present int the final track</param>
        /// <param name="secondary">Only the notes that occur during gaps in the primary track will be kept.</param>
        /// <returns>
        /// The single track that contains all of the primary track and any notes from the secondary track that could be pulled in without overwriting anythign in the primary track.
        /// </returns>
        private NoteTrack MergeTwoTracks(NoteTrack primary, NoteTrack secondary)
        {
            // If there are 4 beats of rest, the event is considered to be the end of the chunk
            int gapTicks = ChartResolution * 4;
            long chunkStartTick = -1;
            long chunkStopTick = -1;
            List<ChartEvent> chunk = new List<ChartEvent>();
            foreach(ChartEvent ev in primary.Notes)
            {
                if (chunkStartTick < 0)
                {
                    chunkStartTick = ev.Tick;
                    chunkStopTick = ev.Tick + ev.Sustain + gapTicks;
                }

                // If this event happens more than 4 beats after the previous event ended then we have reached the end of a chunk
                if (ev.Tick > chunkStopTick)
                {
                    OverwriteChunk(chunk, secondary);
                    chunk.Clear();
                    chunkStartTick = ev.Tick;
                    chunkStopTick = ev.Tick + ev.Sustain + gapTicks;
                }
                else
                {
                    // Push the stop tick value out if we can.
                    if (ev.Tick + ev.Sustain + gapTicks > chunkStopTick)
                    {
                        chunkStopTick = ev.Tick + ev.Sustain + gapTicks;
                    }

                    chunk.Add(ev);
                }
            }

            // Overwrite any notes that were saved in the chunk list.
            OverwriteChunk(chunk, secondary);

            return secondary;
        }

        /// <summary>
        /// Overwrite a chunk of events in the target track.
        /// </summary>
        /// <param name="chunk">
        /// The events that should replace those in the target track.
        /// </param>
        /// <param name="track">The target track to be overwritten.</param>
        private void OverwriteChunk(List<ChartEvent> chunk, NoteTrack track)
        {
            if(chunk == null || chunk.Count == 0)
            {
                return;
            }

            long startTick = chunk.First().Tick;
            long endTick = 0;
            foreach(ChartEvent ev in chunk)
            {
                if (ev.Tick + ev.Sustain > endTick)
                {
                    endTick = ev.Tick + ev.Sustain;
                }
            }

            // Remove all ChartEvents in track with tick >= startTick && tick <= endTick
            List<ChartEvent> merged = track.Notes.Where(n => n.Tick < startTick || n.Tick > endTick).ToList();
            merged.AddRange(chunk);
            track.Notes = merged.OrderBy(n => n.Tick).ToList();
        }

        /// <summary>
        /// Plays the selected Midi track.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlaySelectedTrack_Click(object sender, RoutedEventArgs e)
        {
            NoteTrack item = LeadTrackListView.SelectedItem as NoteTrack;
            if (playSelectedTrack.Content.Equals("Stop Track"))
            {
                foreach (NoteTrack track in LeadTrackListView.Items)
                {
                    if (item.Playback.IsRunning)
                    {
                        item.Playback.Stop();
                    }
                }

                playSelectedTrack.Content = "Start Track";
                return;
            }

            if (item == null)
            {
                return;
            }

            item.Playback.Start();
            playSelectedTrack.Content = "Stop Track";
        }

        /// <summary>
        /// Callback function for when the window loads.
        /// Currently empty but used for debug/testing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ;
        }

        /// <summary>
        /// Callback function for when the Open Audio button is clicked.
        /// </summary>
        /// <param name="sender">
        /// The sender
        /// </param>
        /// <param name="e">
        /// The event arguments.
        /// </param>
        private void BtnOpenAudio_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Audio File|*.mp3;*.ogg;*.wav";
            DialogResult result = fileDialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    string file = fileDialog.FileName;
                    this.AudioFileTextBox.Text = file;
                    AudioFileName = file;
                    this.convertAudioButton.IsEnabled = true;
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }
        }

        /// <summary>
        /// Callback function for when the Convert Audio button is clicked.
        /// This method validates the min/max tempo text boxes and runs an audio conversion task in the thread pool.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The event arguments.
        /// </param>
        private void BtnConvertAudio_Click(object sender, RoutedEventArgs e)
        {
            // Validate the tempo text boxes contain number and they are normal
            string guessText = this.TempoGuessTextBox.Text;
            string errorText = this.TempoErrorTextBox.Text;
            bool parseFailure = false;
            if (!float.TryParse(guessText, out float guessTempo))
            {
                this.TempoGuessTextBox.BorderBrush = Brushes.Red;
                parseFailure = true;
            }
            
            if (!float.TryParse(errorText, out float errorTempo))
            {
                this.TempoErrorTextBox.BorderBrush = Brushes.Red;
                parseFailure = true;
            }

            if (parseFailure)
            {
                return;
            }

            this.TempoGuessTextBox.BorderBrush = Brushes.Black;
            this.TempoErrorTextBox.BorderBrush = Brushes.Black;
            this.convertAudioButton.IsEnabled = false;
            // Trigger some async work hope it goes well
            this.ConvertAudioTask = Task.Run(async () => await ConvertAudioFile(guessTempo - errorTempo, guessTempo + errorTempo).ContinueWith((t) => UpdateConvertButton(true)));
        }

        /// <summary>
        /// This method is responsible for all of the computation required to generate the
        /// tempo track for an audio file. 
        /// 
        /// This method should NEVER be run on the UI thread as it takes a few seconds to run.
        /// </summary>
        /// <param name="minTempo">
        /// The minimum tempo that the tempo detection should use as a hint.
        /// The output tempo may be less than this, this is only a suggestion of where to look.
        /// </param>
        /// <param name="maxTempo">
        /// The maximum tempo that the tempo detection shoudl use as a hint.
        /// The output tempo may be higher than this, this is only a suggestion of where to look.
        /// </param>
        private async Task ConvertAudioFile(float minTempo, float maxTempo)
        {
            await Task.Yield();
            float tempMinTempo = minTempo;
            float tempMaxTempo = maxTempo;

            // BPM Estimation gets pretty wonky when looking for > 200 BPM, so instead check for half the BPM and we can interpolate later.
            if (maxTempo > 200)
            {
                tempMaxTempo /= 2;
                tempMinTempo = Math.Max(40, tempMinTempo / 2);
            }

            using (SampleReader reader = new SampleReader(AudioFileName, 50, (1 << 14)))
            {
                if (reader.SampleRate != 44100)
                {
                    System.Windows.MessageBox.Show($"Unsupported SampleRate = {reader.SampleRate} Hz. Audio file sample rate must equal 44100 Hz. Please resample the audio file using Audacity or something similar.", "Invalid Sample Rate", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateProgressBar("Skipped", 0);
                    return;
                }

                UpdateProgressBar("Reading audio file...", 1);
                float[] signal = reader.ReadAll();

                ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                ////          COMPUTE MELODY
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                ////PredominantPitchMelodia melodyExtractor = new PredominantPitchMelodia(frameSize: 2048, hopSize: 128, minDuration: 100);

                ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                ////          COMPUTE TEMPO TRACK
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                RhythmExtractor extractor = new RhythmExtractor(reader.SampleRate, tempMinTempo, tempMaxTempo);
                (float bpm, float[] ticks, float confidence, float[] estimates, float[] bpmIntervals) = extractor.Compute(signal, UpdateProgressBar);
                (ticks, bpm) = TickPostProcessor.PostProcessTicks(ticks, bpm, minTempo, maxTempo);
                ////(float[] pitches, float[] pitchConfidence) = await melodyTask;
                this.GenerateChartFile(ticks);
            }
        }

        /// <summary>
        /// Generate a chart file from the given beat locations and pitches.
        /// The pitches are optional
        /// </summary>
        /// <param name="ticks">
        /// The exact locations in time of the quarter note beats.
        /// </param>
        /// <param name="pitches">
        /// The pitches of the melody.
        /// </param>
        /// <param name="pitchHopTime">
        /// The amount of real time (seconds) that passes from each array element of the pitches array. (ex. 0.1 seconds from index 0-> index 1)
        /// </param>
        private void GenerateChartFile(float[] ticks, float[] pitches = null, float pitchHopTime = 128f / 44100f)
        {
            UpdateProgressBar("Building .chart File...", 99);
            int chartResolution = 192;
            List<SyncEvent> syncTrack = new List<SyncEvent> { new SyncEvent(0, 1, 4) };
            int currentTick = 0;
            float currentTime = 0;
            for (int i = 0; i < ticks.Length; i++)
            {
                float targetTime = ticks[i];

                // What BPM value will make 192 ticks == tick - currentTime
                float deltaT = targetTime - currentTime;
                double b = 60f / deltaT;
                if (i == 0 || Math.Abs(b - syncTrack[syncTrack.Count - 1].BeatsPerMinute) >= 0.01)
                {
                    // Add the event if its the first event or the BPM changed.
                    syncTrack.Add(new SyncEvent(currentTick, b));
                }

                currentTick = chartResolution * (i + 1);
                currentTime = targetTime;
            }

            List<ChartEvent> noteEvents = new List<ChartEvent>();
            if (pitches != null)
            {
                MovingAverage averager = new MovingAverage(5);
                pitches = averager.Compute(pitches);
                int[] tones = new int[pitches.Length];
                int currentQuarterNote = 0;
                HashSet<long> usedTicks = new HashSet<long>();
                for (int i = 0; i < tones.Length; i++)
                {
                    tones[i] = (int)MathHelpers.HertzToTone(pitches[i]);
                    if (tones[i] == 0)
                    {
                        continue;
                    }

                    float t = i * pitchHopTime;
                    while (currentQuarterNote < ticks.Length - 2 && ticks[currentQuarterNote + 1] < t)
                    {
                        currentQuarterNote++;
                    }

                    if (i == 0 || tones[i] != tones[i - 1])
                    {
                        // Snap tick to 1/32 note
                        float beatStart = currentQuarterNote == 0 ? 0 : ticks[currentQuarterNote];
                        float beatEnd = currentQuarterNote == 0 ? ticks[currentQuarterNote] : ticks[currentQuarterNote + 1];
                        float beatPeriod = beatEnd - beatStart;
                        float fromStartOfBeat = t - beatStart;
                        long tick = (long)(8f * (fromStartOfBeat / beatPeriod));
                        // 32nd tick resolution = 1quarter note resolution / 8.
                        long tRes = chartResolution / 8;
                        tick *= tRes;
                        tick = chartResolution * currentQuarterNote + tick;
                        if (usedTicks.Contains(tick))
                        {
                            continue;
                        }

                        // the tone changed!!! create an event
                        noteEvents.Add(new ChartEvent(tick, tones[i]));
                        usedTicks.Add(tick);
                    }
                }
            }

            NoteTrack track = new NoteTrack(noteEvents, syncTrack, chartResolution, GeneralMidiProgram.ElectricGuitar1, null, "DetectedMelody");
            track.CloneHeroInstrument = CloneHeroInstrument.Single.ToString();
            track.GuitarReshape();

            // Write a .chart file with the sync track filled out
            string outputFile = Path.ChangeExtension(AudioFileName, ".chart");
            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                // Write SONG section
                writer.WriteLine(string.Format(Properties.Resources.SongSection, chartResolution, Path.GetFileName(AudioFileName)));

                // Write SYNC section
                writer.WriteLine(string.Format(Properties.Resources.SyncTrack, string.Join("\n", syncTrack.Select(s => s.ToString()))));


                // Write Note section
                writer.WriteLine(string.Format(Properties.Resources.NoteTrack, $"{CloneHeroDifficulty.Expert}{CloneHeroInstrument.Single}", string.Join("\n", track.Notes.Select(n => n.ToString()))));
            }

            UpdateProgressBar($"Complete -> {outputFile}", 100);
        }

        /// <summary>
        /// Method used for the safe access of UI elements from background threads.
        /// </summary>
        /// <param name="enabled"></param>
        private void UpdateConvertButton(bool enabled = false)
        {
            Dispatcher.Invoke(() => this.convertAudioButton.IsEnabled = enabled);
        }

        /// <summary>
        /// Method used for the safe access of UI elements from background thread.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="progressPercent"></param>
        private void UpdateProgressBar(string label, double progressPercent)
        {
            Dispatcher.Invoke(() =>
            {
                this.ConversionProgress.Value = progressPercent;
                this.StatusText.Text = label;
            });
        }

        /// <summary>
        /// Helper function that returns true only whe a string contains all digits or empty.
        /// </summary>
        /// <param name="text">
        /// The input text to check.
        /// </param>
        /// <returns>
        /// bool indicating whether or not the string was all digits/empty.
        /// </returns>
        private static bool IsTextAllowed(string text)
        {
             if (text == null || text.Length == 0)
            {
                return true;
            }

            foreach(char c in text)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles when text is added to the text box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TempoGuessTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        /// <summary>
        /// Handles when text is added to the text box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TempoErrorTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        /// <summary>
        /// Diplay a help window with information about how the MIDI conversion works.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HelpAudioConversion_Click(object sender, RoutedEventArgs e)
        {
            AudioFileHelp helpWindow = new AudioFileHelp();
            helpWindow.Show();
        }

        /// <summary>
        /// Display a help window with information about how the Audio File Conversion works.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HelpMidiConversion_Click(object sender, RoutedEventArgs e)
        {
            MIDIHelp helpWindow = new MIDIHelp();
            helpWindow.Show();
        }
    }
}
