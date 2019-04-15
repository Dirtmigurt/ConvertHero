

namespace ConvertHero
{
    using ConvertHero.Models;
    using Melanchall.DryWetMidi.Smf;
    using Melanchall.DryWetMidi.Smf.Interaction;
    using Melanchall.DryWetMidi.Standards;
    using Melanchall.DryWetMidi.Tools;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Xml.Linq;
    using System.Windows;

    using System.Windows.Forms;
    using System;
    using System.IO;
    using ConvertHero.Properties;
    using Application = System.Windows.Application;
    using System.Resources;
    using System.Reflection;
    using Melanchall.DryWetMidi.Devices;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static List<SyncEvent> SyncTrack = null;

        private static int ChartResolution = -1;

        private static string MidiFileName;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnOpenMidi_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Midi|*.mid|XML|*.xml";
            DialogResult result = fileDialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    string file = fileDialog.FileName;
                    if (file.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                    {
                        LoadXmlFile(file);
                    }
                    else if (file.EndsWith(".mid", StringComparison.InvariantCultureIgnoreCase))
                    {
                        LoadMidiFile(file);
                    }
                        
                    this.FilenameTextBox.Text = file;
                    MidiFileName = file;
                    startConvertButton.IsEnabled = true;
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }
        }

        private void LoadMidiFile(string fileName)
        {
            MidiFile midiFile = MidiFile.Read(fileName);
            TicksPerQuarterNoteTimeDivision x = midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision;
            ChartResolution = x.TicksPerQuarterNote;
            SyncTrack = LoadSyncEvents(midiFile);
            ObservableCollection<NoteTrack> trackList = LoadNoteEvents(midiFile);
            LeadTrackListView.ItemsSource = trackList;
            BassTrackListView.ItemsSource = new ObservableCollection<NoteTrack>(trackList);
            DrumTrackListView.ItemsSource = new ObservableCollection<NoteTrack>(trackList);
            return;
        }

        private void LoadXmlFile(string fileName)
        {
            XElement doc = XElement.Load(fileName);
        }

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

        private void LeadButtonUP_Click(object sender, RoutedEventArgs e)
        {
            LeadTrackListView.ItemsSource = MoveSelectionUp(LeadTrackListView);
        }

        private void LeadButtonDOWN_Click(object sender, RoutedEventArgs e)
        {
            LeadTrackListView.ItemsSource = MoveSelectionDown(LeadTrackListView);
        }

        private void LeadButtonRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            LeadTrackListView.ItemsSource = RemoveSelection(LeadTrackListView);
        }

        private void BassButtonUP_Click(object sender, RoutedEventArgs e)
        {
            BassTrackListView.ItemsSource = MoveSelectionUp(BassTrackListView);
        }

        private void BassButtonDOWN_Click(object sender, RoutedEventArgs e)
        {
            BassTrackListView.ItemsSource = MoveSelectionDown(BassTrackListView);
        }

        private void BassButtonRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            BassTrackListView.ItemsSource = RemoveSelection(BassTrackListView);
        }

        private void DrumButtonUP_Click(object sender, RoutedEventArgs e)
        {
            DrumTrackListView.ItemsSource = MoveSelectionUp(DrumTrackListView);
        }

        private void DrumButtonDOWN_Click(object sender, RoutedEventArgs e)
        {
            DrumTrackListView.ItemsSource = MoveSelectionDown(DrumTrackListView);
        }

        private void DrumButtonRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            DrumTrackListView.ItemsSource = RemoveSelection(DrumTrackListView);
        }

        private void StartConvertButton_Click(object sender, RoutedEventArgs e)
        {
            // Write expert single for debug now
            using (StreamWriter writer = new StreamWriter(Path.ChangeExtension(MidiFileName, ".chart")))
            {
                // Write lead/bass/drum to file
                // TrackHeader = [{Difficulty}{Instrument}]
                // Difficuly is one of [Expert, Hard, Medium, Easy]
                // Instrument is one of [Single, DoubleBass, Drums]
                // Write SONG section
                writer.WriteLine(string.Format(Properties.Resources.SongSection, ChartResolution, Path.GetFileName(MidiFileName)));

                // Write SYNC section
                writer.WriteLine(string.Format(Properties.Resources.SyncTrack, string.Join("\n", SyncTrack.Select(s => s.ToString()))));

                // Write EVENTS section
                List<string> eventStrings = SyncTrack.Where(s => s.BeatsPerMinute > 0).Select(s => s.ToEventString()).ToList();
                writer.WriteLine(string.Format(Properties.Resources.EventsTrack, string.Join("\n", eventStrings)));

                // LEAD TRACKS
                List<NoteTrack> leadTracks = new List<NoteTrack>();
                foreach(object obj in LeadTrackListView.ItemsSource)
                {
                    leadTracks.Add(obj as NoteTrack);
                }

                if (leadTracks.Count > 0)
                {
                    NoteTrack lead = MergeManyTracks(leadTracks);
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
                    NoteTrack bass = MergeManyTracks(bassTracks);
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
                    NoteTrack drum = MergeManyTracks(drumTracks, true);
                    drum.CloneHeroInstrument = CloneHeroInstrument.Drums.ToString();

                    // Write Note section
                    writer.WriteLine(string.Format(Properties.Resources.NoteTrack, $"{CloneHeroDifficulty.Expert}{CloneHeroInstrument.Drums}", string.Join("\n", drum.Notes.Select(n => n.ToString()))));
                }
            }
        }

        private NoteTrack MergeManyTracks(List<NoteTrack> tracks, bool isDrum=false)
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

            // Reshape the note track to event types 0-5
            if (isDrum)
            {
                // Shape the notes as if they are going to be played on 
                merged.DrumReshape1();
            }
            else
            {
                merged.GuitarReshape1();
                //merged.ExperimentalGuitarReshape(SyncTrack, ChartResolution);
            }

            return merged;
        }

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Can use this for test code/debugging

        }
    }
}
