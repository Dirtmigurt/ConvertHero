namespace ConvertHero.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Melanchall.DryWetMidi.Devices;
    using Melanchall.DryWetMidi.Standards;

    /// <summary>
    /// A NoteTrack contains all of information required to construct a clone hero chart for a single instrument.
    /// It contains the instruments notes, as well as the time signatures and tempos.
    /// </summary>
    public class NoteTrack
    {
        /// <summary>
        /// The Midi instrument that should be playing these notes.
        /// </summary>
        public GeneralMidiProgram Instrument { get; }

        /// <summary>
        ///  The Instrument in CloneHero that should play these notes (Guitar/Bass/Drums)
        /// </summary>
        public string CloneHeroInstrument { get; set; }

        /// <summary>
        /// The list of actual tones played by the instrument in the song.
        /// </summary>
        public List<ChartEvent> Notes = new List<ChartEvent>();

        /// <summary>
        /// The list of time signatures and tempo changes in the song.
        /// </summary>
        public List<SyncEvent> SyncTrack = new List<SyncEvent>();

        /// <summary>
        /// The Midi playback object that allows the track to be played through the UI.
        /// </summary>
        public Playback Playback { get; }

        /// <summary>
        /// The number of ticks per quarter note the track uses.
        /// </summary>
        public int ChartResolution = 192;

        /// <summary>
        /// The minimum (lowest) tone played by the instrument in the track.
        /// </summary>
        public int Min => this.Notes.Select(n => n.Type).Min();

        /// <summary>
        /// The maximum (highest) tone played by the instrument in the track.
        /// </summary>
        public int Max => this.Notes.Select(n => n.Type).Max();

        /// <summary>
        /// Initializes a new instances of the <cref=NoteTrack /> class.
        /// </summary>
        /// <param name="events">
        /// The list of actual tones played by the instrument in the song.
        /// </param>
        /// <param name="syncTrack">
        /// The list of time signatures and tempo changes in the song.
        /// </param>
        /// <param name="chartResolution">
        /// The number of ticks per quarter note that the track uses.
        /// </param>
        /// <param name="instrument">
        /// The midi instrument that should be playing these notes.
        /// </param>
        /// <param name="playback">
        /// The Midi playback object that allows the track to be played through the UI.
        /// </param>
        /// <param name="trackTitle">
        /// The name of the track.
        /// </param>
        public NoteTrack(List<ChartEvent> events, List<SyncEvent> syncTrack, int chartResolution, GeneralMidiProgram instrument, Playback playback, string trackTitle = "Placeholder")
        {
            this.Notes = events;
            this.SyncTrack = syncTrack;
            this.ChartResolution = chartResolution;
            this.Instrument = instrument;
            this.Title = trackTitle;
            this.Playback = playback;
        }

        /// <summary>
        /// The title of this track.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// The number of notes played by the instrument in this track.
        /// </summary>
        public int Count => this.Notes.Count();

        /// <summary>
        /// Each event has a tone from 0 -> 127. These must be mapped down to 0->4 such that
        /// the overall structure of the song remains unchanged.
        /// 
        /// Note that this is impossible to do perfectly as representing tones [0,1,2,3,4] must wrap around somewhere
        /// and there is no absolutely correct way to do this.
        /// 
        /// This method deploys lots of heuristics that can fail in certain situations and produce notes that are jarring 
        /// or inconsistent with the tones being played.
        /// </summary>
        public void GuitarReshape()
        {
            // Break chords into a single note, keeping the number of fingers to use and the Tone span
            BreakChords();

            // Normalize as many note transitions as possible down to single steps
            List<int> notes = LocalNormalization(this.Notes);

            // MAP all notes down to 0-4
            RiseAndFallMapper(this.Notes, 5);

            // Build the chords back out since they were stripped into single notes
            RebuildChords();

            // Fix sustains, since midi files sustain ALL notes
            FixSustains();
        }

        /// <summary>
        /// Each event has a tone from 0 -> 127. These must be mapped down to 0->5 such that
        /// the overall structure of the song remains unchanged.
        /// 
        /// Note that this is impossible to do perfectly as representing tones [0,1,2,3,4,5] must wrap around somewhere
        /// and there is no absolutely correct way to do this.
        /// 
        /// This method deploys lots of heuristics that can fail in certain situations and produce notes that are jarring 
        /// or inconsistent with the tones being played.
        /// </summary>
        public void BassGuitarReshape()
        {
            // Break chords into a single note, keeping the number of fingers to use and the Tone span
            BreakChords();

            // Normalize as many note transitions as possible down to single steps
            List<int> notes = LocalNormalization(this.Notes);

            // MAP all notes down to 0-5
            RiseAndFallMapper(this.Notes, 6);

            // Handle open note replacements
            Change6NotesTo5WithOpen(this.Notes);

            // Build the chords back out since they were stripped into single notes
            RebuildChords();

            // Fix sustains, since midi files sustain ALL notes
            FixSustains();
        }

        /// <summary>
        /// Change the track that uses 6 unique notes to the way clone hero denotes open notes (7).
        /// This just shifts all not values down 1 value so 6 becomes 5, 5 becomes 4, and 0 becomes -1.
        /// 0-5 are valid values for Green/Red/Yellow/Blue/Orange, so we need to change -1 to the value for an open note (7);
        /// </summary>
        /// <param name="notes"></param>
        private void Change6NotesTo5WithOpen(List<ChartEvent> notes)
        {
            foreach(ChartEvent note in notes)
            {
                note.Type = note.Type == 0 ? 7 : note.Type - 1;
            }
        }

        /// <summary>
        /// This method breaks the entires song up into runs where a run is...
        /// 1. A series of notes where the tone is monotonically inreasing
        /// 2. A series of notes where the tone is monotonically decreasing
        /// 
        /// If any of the runs are > 5 they cannot be represented on a clone hero highway without wrapping and so
        /// they are modified.
        /// </summary>
        /// <param name="notes">
        /// </param>
        private void RiseAndFallMapper(List<ChartEvent> notes, int outputTones = 5)
        {
            // Remove duplicate notes
            int prev = -1;
            List<int> dedupedNotes = new List<int>();
            foreach(ChartEvent ev in notes)
            {
                if(ev.Type != prev)
                {
                    dedupedNotes.Add(ev.Type);
                }

                prev = ev.Type;
            }

            // Break song into rises/falls
            // find all inflection points / steps > 1
            SortedSet<int> boundaries = new SortedSet<int> { 0, dedupedNotes.Count-1 };
            for(int i = 1; i < dedupedNotes.Count - 1; i++)
            {
                // if stepping to the next note is > 1 then i is and end and i+1 is a start
                if (Math.Abs(dedupedNotes[i] - dedupedNotes[i+1]) > 1)
                {
                    boundaries.Add(i++);
                    boundaries.Add(i);
                }
                else
                {
                    int backwards = dedupedNotes[i - 1] - dedupedNotes[i];
                    int forwards = dedupedNotes[i] - dedupedNotes[i + 1];
                    if(backwards * forwards <= 0)
                    {
                        boundaries.Add(i);
                    }
                }
            }

            List<int> boundaryList = boundaries.ToList();
            int start = boundaryList[0];
            List<int> run = new List<int>();
            List<List<int>> runs = new List<List<int>>();
            for(int i = 1; i <= boundaryList.Count; i++)
            {
                // catch the edge case on the final index
                if(i == boundaryList.Count)
                {
                    // If the final boundary is a run of length 1 then add it
                    if(start < dedupedNotes.Count)
                    {
                        run.Add(dedupedNotes[start++]);
                        runs.Add(run);
                    }

                    continue;
                }

                int end = boundaryList[i];
                if(start + 1 < end)
                {
                    while(start <= end)
                    {
                        run.Add(dedupedNotes[start++]);
                    }
                    runs.Add(run);
                    run = new List<int>();
                }
                else if (start < end)
                {
                    run.Add(dedupedNotes[start++]);
                    runs.Add(run);
                    run = new List<int>();
                }
            }

            List<int> runMaxes = runs.Select(l => l.Max() + l.Min() / 2).ToList();
            runMaxes = NoteReduceMapping(runMaxes, outputTones);
            for(int i = 0; i < runMaxes.Count; i++)
            {
                // Break the run up
                runs[i] = BreakRun(runs[i], runMaxes[i]);
            }

            // Adjust each run up/down to prevent runs from having the same first/last note
            for(int i = 1; i < runs.Count; i++)
            {
                int lastNote = runs[i - 1].Last();
                if ( runs[i].First() == lastNote)
                {
                    // Can we move this run up/down
                    int runMin = runs[i].Min();
                    int runMax = runs[i].Max();

                    if (runMin > 0)
                    {
                        runs[i] = runs[i].Select(x => x - 1).ToList();
                    }
                    else if(runMax < 4)
                    {
                        runs[i] = runs[i].Select(x => x + 1).ToList();
                    }
                }
            }

            List<int> flattenedRuns = new List<int>();
            foreach(List<int> r in runs)
            {
                flattenedRuns.AddRange(r);
            }

            // Now build the song back up
            prev = -1;
            int pointer = 0;
            List<int> rebuild = new List<int>();
            for(int i = 0; i < notes.Count; i++)
            {
                if (notes[i].Type == prev)
                {
                    rebuild.Add(flattenedRuns[pointer-1]);
                }
                else
                {
                    rebuild.Add(flattenedRuns[pointer++]);
                }

                prev = notes[i].Type;
            }

            for(int i = 0; i < notes.Count; i++)
            {
                notes[i].Type = rebuild[i];
            }
            ;
        }

        /// <summary>
        /// This method breaks a run into smaller runs of less than 5 so that they can fit on a clone hero highway.
        /// </summary>
        /// <param name="run">
        /// The run to be broken.
        /// </param>
        /// <param name="anchor">
        /// The note around which the run should be centered.
        /// This is so that we dont put all runs of length 3 as G+R+Y, that would be a boring song.
        /// </param>
        /// <returns>
        /// The changed run.
        /// </returns>
        public List<int> BreakRun(List<int> run, int anchor)
        {
            int runLength = run.Count;
            bool repeat = true;
            List<int> newNotes = new List<int>();
            while (repeat)
            {
                repeat = false;
                if (runLength < 6)
                {
                    for (int j = 0; j < runLength; j++)
                    {
                        newNotes.Add(j);
                    }
                }
                else if (runLength % 3 == 0)
                {
                    // 0,1,2 | 1,2,3 | 2,3,4
                    for (int j = 0; j < runLength; j++)
                    {
                        newNotes.Add((j % 3) + ((j / 3) % 3));
                    }
                }
                else if (runLength % 7 == 0)
                {
                    // 0,1,2,3 | 2,3,4
                    for (int j = 0; j < runLength; j++)
                    {
                        newNotes.Add((j % 4) + (2 * ((j / 4) % 2)));
                    }
                }
                else if (runLength % 8 == 0)
                {
                    // 0,1,2,3 | 1,2,3,4
                    for (int j = 0; j < runLength; j++)
                    {
                        newNotes.Add((j % 4) + ((j / 4) % 2));
                    }
                }
                else if (runLength % 10 == 0)
                {
                    // 0-4 as many times as needed
                    for (int j = 0; j < runLength; j++)
                    {
                        newNotes.Add((j % 5));
                    }
                }
                else
                {
                    // put an green-> orange run in and see if any of the above patterns will fit.
                    for (int j = 0; j < 5; j++)
                    {
                        newNotes.Add(j);
                    }
                    runLength -= 5;
                    repeat = true;
                }
            }

            if (run.Count > 0 && run.First() > run.Last())
            {
                // all notes were inserted as rising scales, they need to be flipped
                for (int j = 0; j < run.Count; j++)
                {
                    newNotes[j] = -newNotes[j];
                }
            }

            int min = newNotes.Min();
            newNotes = newNotes.Select(n => n - min).ToList();
            int max = newNotes.Max();
            min = newNotes.Min();

            int bump = Math.Min(4 - max, anchor);
            newNotes = newNotes.Select(n => n + bump).ToList();
            return newNotes;
        }

        /// <summary>
        /// Each event has a tone from 35 -> 81. These must be mapped down to 0->4 such that
        /// the overall structure of the song remains unchanged.
        /// 
        /// Note that this is impossible to do perfectly as there are only 2 cymbals and 3 snare/toms on the highway.
        /// 
        /// This method deploys lots of heuristics that can fail in certain situations and produce notes that are jarring 
        /// or inconsistent with the tones being played.
        /// </summary>
        public void DrumReshape()
        {
            //split notes into 3 tracks, kick/cymbal/pad
            List<ChartEvent> kickEvents = new List<ChartEvent>();
            List<ChartEvent> cymbalEvents = new List<ChartEvent>();
            List<ChartEvent> padEvents = new List<ChartEvent>();
            List<ChartEvent> miscEvents = new List<ChartEvent>();
            foreach(ChartEvent ev in this.Notes)
            {
                // Kill the sustains
                ev.Sustain = 0;
                if (PossibleNotes.ContainsKey((GeneralMidiPercussion)ev.Type))
                {
                    int n = PossibleNotes[(GeneralMidiPercussion)ev.Type].Count;
                    switch (n)
                    {
                        case 1:
                            kickEvents.Add(ev);
                            break;
                        case 2:
                            cymbalEvents.Add(ev);
                            break;
                        case 3:
                            padEvents.Add(ev);
                            break;
                        default:
                            miscEvents.Add(ev);
                            break;
                    }
                }
            }

            // MAP KICKS
            foreach(ChartEvent ev in kickEvents)
            {
                ev.Type = (int)DrumType.Kick;
            }

            // MAP CYMBALS (2 notes)
            MapDrumNotes(cymbalEvents, Cymbals);

            // MAP PADS
            MapDrumNotes(padEvents, Pads);
        }

        /// <summary>
        /// This function maps the events to the possible notes on the clone hero highway that are specified.
        /// This is used by passing all of the cymbal events in, and mapping them down to Yellow/Orange or..
        /// Passing all of the Pad events and mapping them to Red/Blue/Green or..
        /// Passing all of the Kick events and mapping them to Kick (very easy)
        /// </summary>
        /// <param name="events">
        /// The events to be changed.
        /// </param>
        /// <param name="possibleNotes">
        /// The notes on the clone hero highway that can be used to represent each event.
        /// </param>
        private void MapDrumNotes(List<ChartEvent> events, List<DrumType> possibleNotes)
        {
            SortedDictionary<int, List<ChartEvent>> measureDictionary = BucketIntoMeasures(events);
            foreach(List<ChartEvent> measure in measureDictionary.Values)
            {
                NormalizeNotes(measure);

                // IF any of the steps between notes == possibleNotes.Count then 
                foreach(ChartEvent note in measure)
                {
                    note.Type = (int)possibleNotes[note.Type % possibleNotes.Count];
                }
            }

            // Fix up some of the obvious errors that can occur
            FixConsistencyErrors(events, possibleNotes.Select(t => (int)t).ToList());
        }

        /// <summary>
        /// Buckets all of the notes into their respective measures within the song.
        /// </summary>
        /// <param name="notes">
        /// The notes in the song.
        /// </param>
        /// <returns>
        /// A Sorted Dictionary where the key is the measure number, and the value is the notes contained in that measure.
        /// </returns>
        private SortedDictionary<int, List<ChartEvent>> BucketIntoMeasures(List<ChartEvent> notes)
        {
            var measureStartTicks = BuildBoundaries();
            // Bucket events by measure
            SortedDictionary<int, List<ChartEvent>> measureDictionary = new SortedDictionary<int, List<ChartEvent>>();
            foreach (ChartEvent cev in notes)
            {
                int measureIndex = GetMeasureFromTick(cev.Tick, measureStartTicks);
                if (measureDictionary.ContainsKey(measureIndex))
                {
                    measureDictionary[measureIndex].Add(cev);
                }
                else
                {
                    measureDictionary[measureIndex] = new List<ChartEvent> { cev };
                }
            }

            return measureDictionary;
        }

        /// <summary>
        /// Buckets all of the notes into their respective tick within the song.
        /// Notes that fall on the same tick are generally reffered to as a chord.
        /// </summary>
        /// <param name="notes">
        /// The notes in the song.
        /// </param>
        /// <returns>
        /// A Sorted Dictionary where the key is the tick, and the value is the notes contained in that chord.
        /// </returns>
        private SortedDictionary<long, List<ChartEvent>> BucketByTicks(List<ChartEvent> notes)
        {
            // Bucket events by tick
            SortedDictionary<long, List<ChartEvent>> tickDictionary = new SortedDictionary<long, List<ChartEvent>>();
            foreach (ChartEvent cev in notes)
            {
                if (tickDictionary.ContainsKey(cev.Tick))
                {
                    tickDictionary[cev.Tick].Add(cev);
                }
                else
                {
                    tickDictionary[cev.Tick] = new List<ChartEvent> { cev };
                }
            }

            return tickDictionary;
        }

        /// <summary>
        /// Reduces the tones down to the minimum amound of tones required to accurately represent the song.
        /// Often times this means using tones 0->25ish, and so if our clone hero controller had 25 buttons there would be no need to process any further.
        /// </summary>
        /// <param name="events">
        /// The notes to change.
        /// </param>
        /// <returns>
        /// The raw integer values of the normalized tones.
        /// </returns>
        private List<int> LocalNormalization(List<ChartEvent> events)
        {
            List<int> noteValues = events.Select(n => n.Type).Distinct().OrderBy(t => t).ToList();
            Dictionary<int, int> NoteTypeRemap = new Dictionary<int, int>();
            for (int i = 0; i < noteValues.Count; i++)
            {
                NoteTypeRemap[noteValues[i]] = i;
            }

            foreach (ChartEvent ev in events)
            {
                ev.Type = NoteTypeRemap[ev.Type];
            }

            List<int> notes = events.Select(n => n.Type).ToList();
            List<int> localNormedNotes = new List<int>();

            // Further normalize by removing holes that dont get filled within 5 unique notes
            int window = 5;
            for(int i = 0; i < notes.Count - 1; i++)
            {
                localNormedNotes.Add(notes[i]);

                // if stepping from i to i-1 > 1 theres a hole... should there be?
                int max = Math.Max(notes[i], notes[i + 1]);
                int min = Math.Min(notes[i], notes[i + 1]);
                HashSet<int> localNotes = new HashSet<int> { notes[i] };
                int j = 1;
                while (localNotes.Count < window)
                {
                    // add notes j steps ahead of i and j steps behind i
                    if (i + j < notes.Count)
                    {
                        localNotes.Add(notes[i + j]);
                    }

                    if (localNotes.Count < window && i - j >= 0)
                    {
                        localNotes.Add(notes[i - j]);
                    }

                    // Searched the WHOLE SONG without finding 5 unique notes smh.
                    if (i + j >= notes.Count && i - j < 0)
                    {
                        break;
                    }

                    j++;
                }

                for (int hole = min + 1; hole < max; hole++)
                {
                    if (!localNotes.Contains(hole))
                    {
                        // move all previous notes up/down to fill the hole
                        int val = hole - notes[i] > 0 ? 1 : -1;
                        for(int k = 0; k < localNormedNotes.Count; k++)
                        {
                            localNormedNotes[k] += val;
                        }
                    }
                }
            }
            localNormedNotes.Add(notes.Last());

            int newMin = localNormedNotes.Min();
            localNormedNotes = localNormedNotes.Select(n => n - newMin).ToList();
            for(int i = 0; i < localNormedNotes.Count; i++)
            {
                events[i].Type = localNormedNotes[i];
            }

            return localNormedNotes;
        }

        /// <summary>
        /// Naively map all notes to their sort order within their local uniqe section.
        /// So if we have a secion 1 2 3 4 5 6 7 8 9 this would map to
        ///                        0 1 2 2 2 2 2 3 4
        ///                        
        /// Note that this may result in repeated 2's when it shouldn't. Effect is reduced
        /// by using these notes as anchor points for the runs, but may still show up.
        /// </summary>
        /// <param name="ogNotes">
        /// The tones to map down to 0->4
        /// </param>
        /// <returns>
        /// The list of mapped notes containing only values 0->4
        /// </returns>
        private List<int> NoteReduceMapping(List<int> ogNotes, int window = 5)
        {
            List<int> rawMapping = new List<int>();
            for (int i = 0; i < ogNotes.Count; i++)
            {
                HashSet<int> localNotes = new HashSet<int> { ogNotes[i] };
                int centerNote = ogNotes[i];
                int j = 1;
                while (localNotes.Count < window)
                {
                    // add notes j steps ahead of i and j steps behind i
                    if (i + j < ogNotes.Count)
                    {
                        localNotes.Add(ogNotes[i + j]);
                    }

                    if (localNotes.Count < window && i - j >= 0)
                    {
                        localNotes.Add(ogNotes[i - j]);
                    }

                    // Searched the WHOLE SONG without finding 5 unique notes smh.
                    if (i + j >= ogNotes.Count && i - j < 0)
                    {
                        break;
                    }

                    j++;
                }

                // How many of those unique notes are < ogNotes[i]
                int current = localNotes.Where(t => t < ogNotes[i]).Count();
                rawMapping.Add(current);
            }

            return rawMapping;
        }

        /// <summary>
        /// Remove all of the chords from the song as having multiple tones per tick is difficult to deal with.
        /// Store some of the information associated with the chord such as the min/max tone in the chord and how many
        /// notes made up the chord so that we can attempt to re-construct it later.
        /// </summary>
        private void BreakChords()
        {
            var tickDictionary = BucketByTicks(this.Notes);
            List<ChartEvent> oneEventPerTick = new List<ChartEvent>();
            foreach(List<ChartEvent> chord in tickDictionary.Values)
            {
                if (chord.Count == 1)
                {
                    oneEventPerTick.Add(chord[0]);
                    continue;
                }

                int chordNote = chord.Max(t => t.Type);
                int minNote = chord.Min(t => t.Type);
                long sustain = chord.Max(t => t.Sustain);

                // Don't ever chart a 5 note chord, thats bullshit
                int chordDegree = Math.Min(chord.Count, 4);
                oneEventPerTick.Add(new ChartEvent(chord[0].Tick, chordNote, sustain, chordDegree, chordNote - minNote));
            }

            this.Notes = oneEventPerTick;
        }

        /// <summary>
        /// Reconstruct all of the chords that were broken into single notes by the BreakChords() method.
        /// This method looks up an appropriate chord mapping based on the number of fingers used and the tonal range of the chord.
        /// This allows two finger chords to be re-built as G+R/G+Y/G+B depending on the tonal range.
        /// </summary>
        private void RebuildChords()
        {
            List<ChartEvent> chords = this.Notes.Where(n => n.Chord > 1).ToList();
            var chordsByMeasure = BucketIntoMeasures(chords);
            foreach(List<ChartEvent> measure in chordsByMeasure.Values)
            {
                int min = measure.Min(c => c.Type);
                int max = measure.Max(c => c.Type);
                int deg = (int)Math.Round(measure.Average(c => c.Chord));
                int width = (int)Math.Round(measure.Average(c => c.ToneSpan));

                // Build mapping
                var map = BuildChordMap(deg, width);

                // for each note in measure, apply mapping
                foreach(ChartEvent ev in measure)
                {
                    List<int> chord = map[ev.Type];
                    // Delete ev from this.Notes
                    this.Notes.Remove(ev);
                    foreach(int note in chord)
                    {
                        this.Notes.Add(new ChartEvent(ev.Tick, note, ev.Sustain));
                    }
                }
            }

            this.Notes = this.Notes.OrderBy(ev => ev.Tick).ToList();
        }

        /// <summary>
        /// Depending on the number of tones and the tonal range of the chord return an appropriate mapping.
        /// A mapping contains the chord that should be inserted for each potential note (G/R/Y/B/O)
        /// </summary>
        /// <param name="degree">
        /// Number of tones in the chord.
        /// </param>
        /// <param name="width">
        /// Tonal range of the chord (Max Tone - Min Tone)
        /// </param>
        /// <returns>
        /// A mapping where the key is a note (Green->Orange) and the value is the chord that should replace that note.
        /// </returns>
        private Dictionary<int, List<int>> BuildChordMap(int degree, int width)
        {
            if (degree <= 2)
            {
                // map will contain chords with 2 buttons pressed
                if (width <= 7)
                {
                    return new Dictionary<int, List<int>> {
                        { (int)NoteType.Green,  new List<int> { 0, 1 } },
                        { (int)NoteType.Red,    new List<int> { 1, 2 } },
                        { (int)NoteType.Yellow, new List<int> { 1, 3 } },
                        { (int)NoteType.Blue,   new List<int> { 2, 3 } },
                        { (int)NoteType.Orange, new List<int> { 3, 4 } }
                    };
                }
                else
                {
                    return new Dictionary<int, List<int>> {
                        { (int)NoteType.Green,  new List<int> { 0, 2 } },
                        { (int)NoteType.Red,    new List<int> { 0, 3 } },
                        { (int)NoteType.Yellow, new List<int> { 1, 3 } },
                        { (int)NoteType.Blue,   new List<int> { 1, 4 } },
                        { (int)NoteType.Orange, new List<int> { 2, 4 } }
                    };
                }
            }
            else
            {
                if (width <= 14)
                {
                    // map will contain chords with 3 buttons pressed
                    return new Dictionary<int, List<int>> {
                        { (int)NoteType.Green,  new List<int> { 0, 1, 2 } },
                        { (int)NoteType.Red,    new List<int> { 0, 2, 3 } },
                        { (int)NoteType.Yellow, new List<int> { 1, 2, 3 } },
                        { (int)NoteType.Blue,   new List<int> { 1, 3, 4 } },
                        { (int)NoteType.Orange, new List<int> { 2, 3, 4 } }
                    };
                }
                else
                {
                    // map will contain chords with 3 buttons pressed
                    return new Dictionary<int, List<int>> {
                        { (int)NoteType.Green,  new List<int> { 0, 1, 3 } },
                        { (int)NoteType.Red,    new List<int> { 0, 2, 3 } },
                        { (int)NoteType.Yellow, new List<int> { 1, 2, 3 } },
                        { (int)NoteType.Blue,   new List<int> { 1, 2, 4 } },
                        { (int)NoteType.Orange, new List<int> { 1, 3, 4 } }
                    };
                }
            }
        }

        /// <summary>
        /// This function removes excess sustains in the track.
        /// </summary>
        private void FixSustains()
        {
            // Only keep sustains if you hold them for > 1 beat
            int minSustain = this.ChartResolution * 1;
            foreach(ChartEvent ev in this.Notes)
            {
                if (ev.Sustain < minSustain)
                {
                    ev.Sustain = 0;
                }
                else
                {
                    // Dont let sustains run into the next note chop off 1/16th note
                    ev.Sustain -= this.ChartResolution / 4;
                }
            }
        }

        /// <summary>
        /// Attempts to fix some of the common errors that come out of the drum mappings like...
        /// 1. Different cymbal notes that both mapped to the same color and are adjacent in the song. (This is the most difficult one)
        /// 2. Chords where two tones within the chord map to the same color giving the illusion that it is not a chord
        /// 3. If two adjacent tones are equal they MUST map to the same color (This conflicts with #1)
        /// 
        /// This method is not perfect and only fixes these issues some of the time.
        /// </summary>
        /// <param name="events">
        /// The list of notes to change.
        /// </param>
        /// <param name="possibleNotes">
        /// The potential notes that each event can map to.
        /// </param>
        private void FixConsistencyErrors(List<ChartEvent> events, List<int> possibleNotes)
        {
            SortedDictionary<long, List<ChartEvent>> notesByTick = BucketByTicks(events);
            Dictionary<int, int> previousToneToNoteDictionary = new Dictionary<int, int>();
            Dictionary<int, int> currentToneToNoteDictionary = new Dictionary<int, int>();
            HashSet<int> previousNotesUsed = new HashSet<int>();
            HashSet<int> currentNotesUsed = new HashSet<int>();
            foreach (KeyValuePair<long, List<ChartEvent>> chord in notesByTick)
            {
                // Fix overlaps within the chord
                HashSet<int> unused = new HashSet<int>(possibleNotes);
                foreach(ChartEvent note in chord.Value)
                {
                    if(!unused.Contains(note.Type) && unused.Count > 0)
                    {
                        // A Previous note used this type up, pick the closes unused note.
                        int distance = int.MaxValue;
                        int n = -1;
                        foreach(int unusedNote in unused)
                        {
                            if (Math.Abs(note.Type - unusedNote) < distance)
                            {
                                n = unusedNote;
                            }
                        }
                        note.Type = n;
                    }

                    unused.Remove(note.Type);
                }

                // Make sure that If a tone exists in chord and previousTick, that the same note exists in both as well
                foreach(ChartEvent note in chord.Value)
                {
                    if (previousToneToNoteDictionary.ContainsKey(note.Tone))
                    {
                        note.Type = previousToneToNoteDictionary[note.Tone];
                    }
                    // Make sure that if a note exists in chord and previousTick they are the same tone
                    else if (previousNotesUsed.Contains(note.Type))
                    {
                        // this tone wasnt in the prev tick but it got mapped to a note used in the previous tick, so pick a different one
                        HashSet<int> newNotes = new HashSet<int>(possibleNotes);
                        newNotes.ExceptWith(previousNotesUsed);

                        // All of the notes are taken so who cares
                        if (newNotes.Count == 0)
                        {
                            continue;
                        }

                        note.Type = newNotes.ToList()[note.Type % newNotes.Count];
                    }
                }
            }
        }

        /// <summary>
        /// Reduce the tone values down to the minimum set of tones required to still accurately represent the track.
        /// </summary>
        /// <param name="events">
        /// The events to change.
        /// </param>
        private void NormalizeNotes(IEnumerable<ChartEvent> events)
        {
            List<int> noteValues = events.Select(n => n.Type).Distinct().OrderBy(t => t).ToList();
            Dictionary<int, int> NoteTypeRemap = new Dictionary<int, int>();
            for (int i = 0; i < noteValues.Count; i++)
            {
                NoteTypeRemap[noteValues[i]] = i;
            }

            foreach (ChartEvent ev in events)
            {
                ev.Type = NoteTypeRemap[ev.Type];
            }
        }

        /// <summary>
        /// Returns a list of ticks where each tick is the start of a measure.
        /// </summary>
        /// <returns>
        /// A list of ticks where each tick is the start of a measure.
        /// </returns>
        private List<long> BuildBoundaries()
        {
            long maxTick = this.Notes.Max(n => n.Tick);
            List<long> measureStartTicks = new List<long>();
            // Now add new ticks until tick > maxTick
            long newMeasureTick = 0;
            while (newMeasureTick < maxTick + (4 * this.ChartResolution))
            {
                measureStartTicks.Add(newMeasureTick);
                newMeasureTick += this.ChartResolution * 4; // * 4 / sev.Denominator; // ?????????
            }

            foreach (SyncEvent sev in this.SyncTrack)
            {
                // Ignore BPM Events they do not affect the tick size of a measure.
                if (sev.BeatsPerMinute > 0)
                {
                    continue;
                }

                // remove aalll measureStartTicks from the list that are > sev.Tick
                for (int i = measureStartTicks.Count - 1; i >= 0; i--)
                {
                    if (measureStartTicks[i] >= sev.Tick)
                    {
                        measureStartTicks.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }

                // Now add new ticks until tick > maxTick
                newMeasureTick = sev.Tick;
                while (newMeasureTick < maxTick + (4 * this.ChartResolution))
                {
                    measureStartTicks.Add(newMeasureTick);
                    newMeasureTick += this.ChartResolution * sev.Numerator; // * 4 / sev.Denominator; // ?????????
                }
            }

            return measureStartTicks;
        }

        /// <summary>
        /// Look up which measure the specified tick falls into.
        /// </summary>
        /// <param name="tick">
        /// The tick to look up.
        /// </param>
        /// <param name="measureStartTicks">
        /// The list of measure start ticks.
        /// </param>
        /// <returns>
        /// The measure number that contains the tick.
        /// </returns>
        private int GetMeasureFromTick(long tick, List<long> measureStartTicks)
        {
            for(int i = 0; i < measureStartTicks.Count; i++)
            {
                if (measureStartTicks[i] > tick)
                {
                    return i - 1;
                }
                else if (tick == measureStartTicks[i])
                {
                    return i;
                }
            }

            // This should never happen.
            return -1;
        }

        /// <summary>
        /// List of potential clone hero notes that can be used for a drum type.
        /// In this case only the kick (open note) can be used for kick events.
        /// </summary>
        private static readonly List<DrumType> Kick = new List<DrumType> { DrumType.Kick };

        /// <summary>
        /// List of potential clone hero notes that can be used for a drum type.
        /// In this case the Red/Blue/Green notes can be used for pad events.
        /// </summary>
        private static readonly List<DrumType> Pads = new List<DrumType> { DrumType.Red, DrumType.Blue, DrumType.Green };

        /// <summary>
        /// List of potential clone hero notes that can be used for a drum type.
        /// In this case the Yellow/Orange notes can be used for cymbal events.
        /// </summary>
        private static readonly List<DrumType> Cymbals = new List<DrumType> { DrumType.Yellow, DrumType.Orange };

        /// <summary>
        /// List of potential clone hero notes that can be used for a drum type.
        /// In this case the Red/Blue/Green/Yellow/Orange notes can be used for miscellaneous events.
        /// </summary>
        private static readonly List<DrumType> NonKicks = new List<DrumType> { DrumType.Red, DrumType.Yellow, DrumType.Blue, DrumType.Orange, DrumType.Green };

        /// <summary>
        /// These are the supported Midi drum event types and which clone hero notes they can potentially be represented as.
        /// </summary>
        public static Dictionary<GeneralMidiPercussion, List<DrumType>> PossibleNotes = new Dictionary<GeneralMidiPercussion, List<DrumType>>
        {
            { (GeneralMidiPercussion)35, Kick },
            { (GeneralMidiPercussion)36, Kick },
            { (GeneralMidiPercussion)37, Pads },
            { (GeneralMidiPercussion)38, Pads },
            { (GeneralMidiPercussion)39, Pads },
            { (GeneralMidiPercussion)40, Pads },
            { (GeneralMidiPercussion)41, Pads },
            { (GeneralMidiPercussion)42, Cymbals },
            { (GeneralMidiPercussion)43, Pads },
            { (GeneralMidiPercussion)44, Cymbals },
            { (GeneralMidiPercussion)45, Pads },
            { (GeneralMidiPercussion)46, Cymbals },
            { (GeneralMidiPercussion)47, Pads },
            { (GeneralMidiPercussion)48, Pads },
            { (GeneralMidiPercussion)49, Cymbals },
            { (GeneralMidiPercussion)50, Pads },
            { (GeneralMidiPercussion)51, Cymbals },
            { (GeneralMidiPercussion)52, Cymbals },
            { (GeneralMidiPercussion)53, Cymbals },
            { (GeneralMidiPercussion)54, Cymbals },
            { (GeneralMidiPercussion)55, Cymbals },
            { (GeneralMidiPercussion)56, Cymbals },
            { (GeneralMidiPercussion)57, Cymbals },
            { (GeneralMidiPercussion)58, Pads },
            { (GeneralMidiPercussion)59, Cymbals },
            { (GeneralMidiPercussion)60, Pads },
            { (GeneralMidiPercussion)61, Pads },
            { (GeneralMidiPercussion)62, Pads },
            { (GeneralMidiPercussion)63, Pads },
            { (GeneralMidiPercussion)64, Pads },
            { (GeneralMidiPercussion)65, Pads },
            { (GeneralMidiPercussion)66, Pads },
            { (GeneralMidiPercussion)67, Cymbals },
            { (GeneralMidiPercussion)68, Cymbals },
            { (GeneralMidiPercussion)69, Pads },
            { (GeneralMidiPercussion)70, Pads },
            { (GeneralMidiPercussion)71, Pads },
            { (GeneralMidiPercussion)72, Pads },
            { (GeneralMidiPercussion)73, Pads },
            { (GeneralMidiPercussion)74, Pads },
            { (GeneralMidiPercussion)75, Pads },
            { (GeneralMidiPercussion)76, Cymbals },
            { (GeneralMidiPercussion)77, Cymbals },
            { (GeneralMidiPercussion)78, Pads },
            { (GeneralMidiPercussion)79, Pads },
            { (GeneralMidiPercussion)80, Cymbals },
            { (GeneralMidiPercussion)81, Cymbals }
        };
    }
}
