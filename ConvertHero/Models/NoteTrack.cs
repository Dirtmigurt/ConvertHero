using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.Models
{
    using Melanchall.DryWetMidi.Devices;
    using Melanchall.DryWetMidi.Standards;

    public class NoteTrack
    {
        public GeneralMidiProgram Instrument { get; }
        public string CloneHeroInstrument { get; set; }
        public List<ChartEvent> Notes = new List<ChartEvent>();
        public List<SyncEvent> SyncTrack = new List<SyncEvent>();
        public Playback Playback { get; }
        public static List<int[]> possibilities = new List<int[]>();
        public int ChartResolution = 192;
        public int Min => this.Notes.Select(n => n.Type).Min();
        public int Max => this.Notes.Select(n => n.Type).Max();

        public NoteTrack(List<ChartEvent> events, List<SyncEvent> syncTrack, int chartResolution, GeneralMidiProgram instrument, Playback playback, string trackTitle = "Placeholder")
        {
            this.Notes = events;
            this.SyncTrack = syncTrack;
            this.ChartResolution = chartResolution;
            this.Instrument = instrument;
            this.Title = trackTitle;
            this.Playback = playback;
            // Compute min/max tones in the song
            List<int> noteValues = this.Notes.Select(n => n.Type).ToList();
        }

        public string Title { get; }

        public int Count => this.Notes.Count();

        public bool Supported => IgnoredInstrument((int)this.Instrument);

        static bool IgnoredInstrument(int programNumber)
        {
            // Guitar Pro keeps exporting drum tracks as 0 so ignore them.
            if (programNumber == 0)
            {
                return false;
            }

            // Dont look at drum tracks
            if (programNumber >= 112)
            {
                return false;
            }

            // Don't look at bass tracks
            if (programNumber >= 32 && programNumber <= 39)
            {
                return false;
            }

            // Dont look at orchestral wind instruments
            if (programNumber >= 42 && programNumber <= 71)
            {
                return false;
            }

            // Dont look at orchestral wind instruments
            if (programNumber >= 42 && programNumber <= 71)
            {
                return false;
            }

            return true;
        }

        public void DrumReshape()
        {
            // NEED TO PREVENT EVENTS ON THE SAME TICK FROM MAPPING TO THE SAME NOTE
            List<ChartEvent> currentTickEvents = new List<ChartEvent>();
            HashSet<DrumType> currentTickNotesTaken = new HashSet<DrumType>();
            List<ChartEvent> previousTickEvents = new List<ChartEvent>();
            HashSet<DrumType> previousTickNotesTaken = new HashSet<DrumType>();
            Dictionary<int, DrumType> currentTickToneMap = new Dictionary<int, DrumType>();
            Dictionary<int, DrumType> previousTickToneMap = new Dictionary<int, DrumType>();
            foreach (ChartEvent ev in this.Notes)
            {
                #region CurrentTickDedupe
                if (currentTickEvents.Count > 0 && ev.Tick != currentTickEvents.First().Tick)
                {
                    previousTickNotesTaken = currentTickNotesTaken;
                    previousTickEvents = currentTickEvents;
                    previousTickToneMap = currentTickToneMap;

                    currentTickEvents = new List<ChartEvent>();
                    currentTickNotesTaken = new HashSet<DrumType>();
                    currentTickToneMap = new Dictionary<int, DrumType>();
                }

                // If this tone is the same as the tick before, it should be the same note, unless that note is already taken this tick, then go wild.
                if (previousTickToneMap.ContainsKey(ev.Tone) && !currentTickNotesTaken.Contains(previousTickToneMap[ev.Tone]))
                {
                    ev.Type = (int)previousTickToneMap[ev.Tone];
                }
                // If this tone is different than the tick before, it should NOT map to the same note unless all other notes are taken
                else if (PossibleNotes.ContainsKey((GeneralMidiPercussion)ev.Type))
                {
                    HashSet<DrumType> possibleNotes = new HashSet<DrumType>(PossibleNotes[(GeneralMidiPercussion)ev.Type]);
                    possibleNotes.ExceptWith(currentTickNotesTaken);
                    possibleNotes.ExceptWith(previousTickNotesTaken);
                    if (possibleNotes.Count == 0)
                    {
                        possibleNotes = new HashSet<DrumType>(PossibleNotes[(GeneralMidiPercussion)ev.Type]);
                    }

                    int index = ev.Type % possibleNotes.Count;
                    ev.Type = (int)PossibleNotes[(GeneralMidiPercussion)ev.Type][index];
                }
                else
                {
                    HashSet<DrumType> possibleNotes = new HashSet<DrumType>(NonKicks);
                    possibleNotes.ExceptWith(currentTickNotesTaken);
                    possibleNotes.ExceptWith(previousTickNotesTaken);
                    List<DrumType> pbList = null;
                    if (possibleNotes.Count == 0)
                    {
                        pbList = new List<DrumType>(NonKicks);
                    }
                    else
                    {
                        pbList = possibleNotes.ToList();
                    }

                    int index = ev.Type % pbList.Count;
                    ev.Type = (int)pbList[index];
                }

                #endregion

                // If any of the previousTickEvents have same tone, but different Type, then thats vbad
                // Sorry there is no such thing as a drum sustain
                ev.Sustain = 0;
                currentTickEvents.Add(ev);
                currentTickNotesTaken.Add((DrumType)ev.Type);
                currentTickToneMap.Add(ev.Tone, (DrumType)ev.Type);
            }
        }

        public void GuitarReshape()
        {
            // Break chords
            this.BreakChords();
            List<int> normalized = GetIncrements(this.Notes);
            for(int i = 0; i < normalized.Count; i++)
            {
                this.Notes[i].Type = normalized[i] % 5;
                this.Notes[i].Sustain = Math.Max(0, this.Notes[i].Sustain - (this.ChartResolution / 32));

                for(int j = 1; j < this.Notes[i].Chord; j++)
                {
                    // Add a note that is smaller than this.Note[i].Type
                    int note = (this.Notes[i].Type + 4) % 5;
                }
            }
        }

        public void GuitarReshape1()
        {
            // Break chords into a single note, keeping the number of fingers to use and the Tone span
            BreakChords();

            // Normalize as many note transitions as possible down to single steps
            //List<int> notes = LocalNormalization(this.Notes);

            // MAP all notes down to 0-4
            BestGuessMapper(this.Notes);

            // If there are any neighboring notes with equal Type, but differnt TONE, they must be fixed! (This happens in long runs/solos)
            FixNormalizationErrors(this.Notes);

            // Build the chords back out since they were stripped into single notes
            RebuildChords();

            // Fix sustains, since midi files sustain ALL notes
            FixSustains();
        }

        public void DrumReshape1()
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
                int n = PossibleNotes[(GeneralMidiPercussion)ev.Type].Count;
                switch(n)
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

        private void MapDrumNotes(List<ChartEvent> events, List<DrumType> possibleNotes)
        {
            SortedDictionary<int, List<ChartEvent>> measureDictionary = BucketIntoMeasures(events);
            foreach(List<ChartEvent> measure in measureDictionary.Values)
            {
                NormalizeNotes(measure);
                List<int> inc = GetIncrements(measure);
                List<int> steps = GetSteps(measure);
                if (steps.Any(s => s > 0 && s % possibleNotes.Count == 0))
                {
                    ;
                }

                // IF any of the steps between notes == possibleNotes.Count then 
                foreach(ChartEvent note in measure)
                {
                    note.Type = (int)possibleNotes[note.Type % possibleNotes.Count];
                }
            }

            // Fix up some of the obvious errors that can occur
            FixConsistencyErrors(events, possibleNotes.Select(t => (int)t).ToList());
        }

        public void MeasureReshape(List<ChartEvent> currentMeasure, int cmi, List<ChartEvent> previousMeasure, int pmi)
        {
            #region Remove Holes
            List<int> noteValues = currentMeasure.Select(n => n.Type).Distinct().OrderBy(t => t).ToList();
            Dictionary<int, int> NoteTypeRemap = new Dictionary<int, int>();
            for (int i = 0; i < noteValues.Count; i++)
            {
                NoteTypeRemap[noteValues[i]] = i;
            }

            foreach (ChartEvent ev in currentMeasure)
            {
                ev.Type = NoteTypeRemap[ev.Type];
            }
            #endregion

            List<NoteType> possibleFirstNotes = new List<NoteType> { NoteType.Green, NoteType.Red, NoteType.Yellow, NoteType.Blue, NoteType.Orange };
            if (pmi > 0 && pmi + 1 == cmi)
            {
                // Measure are back to back and prev.Last must not break continuity with current.First
                if (previousMeasure.Last().Tone == currentMeasure.First().Tone)
                {
                    // We HAVE to use the same note value for our first note as the last note of the previous measure
                    possibleFirstNotes = new List<NoteType> { (NoteType)previousMeasure.Last().Type };
                }
                else
                {
                    // We CANNOT use the same note value for our first note
                    possibleFirstNotes.Remove((NoteType)previousMeasure.Last().Type);
                }
            }

            // DO SOME MAGIC
            int min = currentMeasure.Min(t => t.Type);
            int max = currentMeasure.Max(t => t.Type);
            if (max > (int)NoteType.Orange)
            {
                ;
            }

            if (!possibleFirstNotes.Contains((NoteType)currentMeasure.First().Type))
            {
                // If this is a chord we can just pick a different chord
                // Can we shift all notes up/down to get them to match?
                // Can we shift all the notes in the previous measure?

            }

            // if Max < 4 we, can slide the measure up n steps, should we?
        }

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

        private void FixNormalizationErrors(List<ChartEvent> events)
        {
            int mod6Count = 0;
            List<int> types = events.Select(e => e.Type).ToList();
            for(int i = 0; i < events.Count - 1; i++)
            {
                if (events[i].Type == events[i+1].Type && events[i].Tone  != events[i+1].Tone)
                {
                    // We found some bad notes!
                    // I guess these arent always in a nice run :(
                    List<ChartEvent> run = GetCurrentRun(events, i);
                    int runLength = run.Count;
                    bool repeat = true;
                    List<int> newNotes = new List<int>();
                    while(repeat)
                    {
                        repeat = false;
                        if (runLength < 6)
                        {
                            // This should be impossible....
                            ;
                        }
                        else if (runLength % 6 == 0)
                        {
                            if (mod6Count % 2 == 0)
                            {
                                // 0,1,3 | 1,2,4
                                newNotes.AddRange(new List<int> { 0, 1, 3, 1, 2, 4 });
                            }
                            else
                            {
                                // 0,2,3 | 1,3,4
                                newNotes.AddRange(new List<int> { 0, 2, 3, 1, 3, 4 });
                            }
                            mod6Count++;
                        }
                        else if (runLength % 7 == 0)
                        {
                            // 0,1,2,3 | 2,3,4
                            for (int j = 0; j < 7; j++)
                            {
                                newNotes.Add((j % 4) + (2 * (j / 4)));
                            }
                        }
                        else if (runLength % 8 == 0)
                        {
                            // 0,1,2,3 | 1,2,3,4
                            for (int j = 0; j < 8; j++)
                            {
                                newNotes.Add((j % 4) + (j / 4));
                            }
                        }
                        else if (runLength % 9 == 0)
                        {
                            // 0,1,2 | 1,2,3 | 2,3,4
                            for (int j = 0; j < 9; j++)
                            {
                                newNotes.Add((j % 3) + (j / 3));
                            }
                        }
                        else if (runLength % 10 == 0)
                        {
                            // 0-4 as many times as needed
                            for (int j = 0; j < 10; j++)
                            {
                                newNotes.Add((j % 5) + (j / 5));
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

                    if (run.Count > 0 && run.First().Type > run.Last().Type)
                    {
                        // all notes were inserted as rising scales, they need to be flipped
                        for(int j = 0; j < run.Count; j++)
                        {
                            newNotes[j] = 4 - newNotes[j];
                        }
                    }
                    
                    // Overwrite the run now
                    for(int j = 0; j < newNotes.Count; j++)
                    {
                        run[j].Type = newNotes[j];
                    }
                }
            }
        }

        private static List<ChartEvent> GetCurrentRun(List<ChartEvent> events, int index)
        {
            List<ChartEvent> run = new List<ChartEvent>();
            if (index >= events.Count)
            {
                return run;
            }

            // back up until we can pick a direction
            int i = index;
            while (i > 0 && events[i].Type == events[index].Type)
            {
                i--;
            }

            // left index - right index
            int backdirection = events[i].Type - events[index].Type;
            while (i >= 0 &&
                (events[i].Type - events[Math.Min(i + 1, index)].Type) * backdirection >= 0)
            {
                i--;
            }
            i++;

            // step forward until we can pick a direction
            int j = index;
            while (j < events.Count - 1 && events[j].Type == events[index].Type)
            {
                j++;
            }

            // Left index - right index
            int forwardDirection = events[index].Type - events[j].Type;

            // If they both have a real, non-zero direction and are opposite
            // two positives = +1
            // two negatives = +1
            // any zero = 0
            if (forwardDirection * backdirection < 0)
            {
                return run;
            }

            while (j < events.Count &&
                (events[Math.Max(index, j - 1)].Type - events[j].Type) * forwardDirection >= 0)
            {
                j++;
            }
            j--;

            // Indices i->j contain the run
            for (int k = i; k <= j; k++)
            {
                run.Add(events[k]);
            }

            return run;
        }

        private void BestGuessMapper(List<ChartEvent> events)
        {
            List<int> ogNotes = events.Select(n => n.Type).ToList();
            List<int> rawMapping = new List<int>();
            int window = 5;
            for (int i = 0; i < ogNotes.Count; i++)
            {
                HashSet<int> localNotes = new HashSet<int> { ogNotes[i] };
                int centerNote = ogNotes[i];
                long centerTick = events[i].Tick;
                int j = 1;
                while(localNotes.Count < window)
                {
                    // add notes j steps ahead of i and j steps behind i
                    if (i + j < ogNotes.Count)
                    {
                        // Dont include jumps > 1/2 octave or notes > 2 measures away
                        if (Math.Abs(centerNote - ogNotes[i+j]) < 6 && Math.Abs(centerTick - events[i+j].Tick) < 8 * ChartResolution)
                        {
                            localNotes.Add(ogNotes[i + j]);
                        }
                    }

                    if (localNotes.Count < window && i - j >= 0)
                    {
                        // Dont include jumps > 1/2 octave, or notes > 2 measures away
                        if (Math.Abs(centerNote - ogNotes[i - j]) < 6 && Math.Abs(centerTick - events[i - j].Tick) < 8 * ChartResolution)
                        {
                            localNotes.Add(ogNotes[i - j]);
                        }
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
                int prev = i > 0 ? localNotes.Where(t => t < ogNotes[i - 1]).Count() : current;
                int translation = (events[i - 1].Type + (current - prev + 5)) % 5;

                // If the current note is mapped the same button as the previous note AND they are different, then we need to nudge up/down
                if (i > 0 && rawMapping[i] == rawMapping[i-1] && ogNotes[i - 1] == ogNotes[i])
                {
                    events[i].Type = translation;
                }
                // If the current note is the same tone as the previous note then we need to make them meatch
                else if (i > 0 && ogNotes[i] == ogNotes[i-1])
                {
                    events[i].Type = events[i - 1].Type;
                }
                else
                {
                    events[i].Type = current;
                }
            }
        }

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
                var map = BuildChordMap(max - min, deg, width);

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
        }

        private Dictionary<int, List<int>> BuildChordMap(int range, int degree, int width)
        {
            if (degree <= 2)
            {
                // map will contain chords with 2 buttons pressed
                if (width <= 6)
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

        private int GetLengthOfLongestRiseOrFall(List<ChartEvent> notes)
        {
            List<int> increments = GetIncrements(notes);
            int max = 0;
            int currentLength = 1;
            for(int i = 1; i < increments.Count; i++)
            {
                if (increments[i] == 0)
                {
                    max = Math.Max(max, currentLength);
                    currentLength = 1;
                }
                else if (increments[i] == increments[i-1])
                {
                    currentLength++;
                }
                else
                {
                    max = Math.Max(max, currentLength);
                    currentLength = 1;
                }
            }

            return Math.Max(currentLength, max);
        }

        private List<int> GetSteps(List<ChartEvent> notes)
        {
            List<int> steps = new List<int>();
            for(int i = 0; i < notes.Count - 1; i++)
            {
                steps.Add(Math.Abs(notes[i].Type - notes[i+1].Type));
            }

            return steps;
        }

        private List<int> GetIncrements(List<ChartEvent> notes)
        {
            List<int> inc = new List<int>();
            for (int i = 0; i < notes.Count - 1; i++)
            {
                if (notes[i].Type < notes[i + 1].Type)
                {
                    inc.Add(1);
                }
                else if (notes[i].Type > notes[i + 1].Type)
                {
                    inc.Add(-1);
                }
                else
                {
                    inc.Add(0);
                }
            }

            return inc;
        }

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

        private static List<DrumType> Kick = new List<DrumType> { DrumType.Kick };

        private static List<DrumType> Pads = new List<DrumType> { DrumType.Red, DrumType.Blue, DrumType.Green };

        private static List<DrumType> Cymbals = new List<DrumType> { DrumType.Yellow, DrumType.Orange };

        private static List<DrumType> NonKicks = new List<DrumType> { DrumType.Red, DrumType.Yellow, DrumType.Blue, DrumType.Orange, DrumType.Green };

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
