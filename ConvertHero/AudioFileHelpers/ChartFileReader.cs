namespace ConvertHero.AudioFileHelpers
{
    using ConvertHero.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Class that is capable of interpreting the .chart file format used by CloneHero.
    /// </summary>
    public static class ChartFileReader
    {
        /// <summary>
        /// Create a 2d array of note events from the specified .chart file.
        /// </summary>
        /// <param name="chartFile">
        /// The input chart file.
        /// </param>
        /// <param name="frameRate">
        /// The frame rate of the output.
        /// </param>
        /// <returns></returns>
        public static float[,] GetChartFrames(string chartFile, int frameRate)
        {
            int chartResolution = 192;
            double chartOffset = 0.0;
            SortedDictionary<long, ChartEvent> noteEvents = new SortedDictionary<long, ChartEvent>();
            List<SyncEvent> SyncTrack = new List<SyncEvent>();
            using (StreamReader chartReader = new StreamReader(chartFile))
            {
                string line = null;
                while ((line = chartReader.ReadLine()) != null)
                {
                    line.Trim();
                    if (line.Equals("[Song]"))
                    {
                        // Get resolution and offset values
                        while ((line = chartReader.ReadLine()) != "}" && line != null)
                        {
                            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length < 3)
                            {
                                continue;
                            }

                            if (tokens[0].Equals("Resolution", StringComparison.InvariantCultureIgnoreCase))
                            {
                                int.TryParse(tokens[2], out chartResolution);
                            }

                            if (tokens[0].Equals("Offset", StringComparison.InvariantCultureIgnoreCase))
                            {
                                double.TryParse(tokens[2], out chartOffset);
                            }
                        }
                    }

                    if (line.Equals("[SyncTrack]"))
                    {
                        // Get BPM values and their corresponding ticks
                        while ((line = chartReader.ReadLine()) != "}" && line != null)
                        {
                            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length < 4)
                            {
                                continue;
                            }

                            int tick;
                            if (!int.TryParse(tokens[0], out tick))
                            {
                                continue;
                            }

                            if (tokens[2].Equals("B", StringComparison.InvariantCultureIgnoreCase))
                            {
                                // add this event to the track
                                int bpm;
                                if (!int.TryParse(tokens[3], out bpm))
                                {
                                    continue;
                                }

                                SyncTrack.Add(new SyncEvent(tick, bpm / 1000.0));
                            }
                        }
                    }

                    // May want to read in bass/drums in the future?
                    if (line.Equals("[ExpertSingle]"))
                    {
                        // Get all notes and their tick locations
                        while ((line = chartReader.ReadLine()) != "}" && line != null)
                        {
                            ChartEvent chartEvent;
                            if (ChartEvent.TryParse(line, out chartEvent))
                            {
                                if (noteEvents.ContainsKey(chartEvent.Tick))
                                {
                                    // This is a chord or has mods, so merge the events
                                    ChartEvent existing = noteEvents[chartEvent.Tick];
                                    existing.Type |= (1 << chartEvent.Type);
                                    if (chartEvent.Sustain > existing.Sustain)
                                    {
                                        existing.Sustain = chartEvent.Sustain;
                                    }
                                }
                                else
                                {
                                    chartEvent.Type = (1 << chartEvent.Type);
                                    noteEvents.Add(chartEvent.Tick, chartEvent);
                                }
                            }
                        }
                    }
                }
            }

            List<Event> eventList = new List<Event>();
            eventList.AddRange(SyncTrack);
            eventList.AddRange(noteEvents.Values);
            eventList = eventList.OrderBy(e => e.Tick).ToList();

            // Build a list of note events with absolute time/ not ticks
            double tickDelta = 0;//60.0 / (this.chartResolution * beatsPerMinute);
            double timePointer = chartOffset;
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
                    tickDelta = 60 / (chartResolution * syncEvent.BeatsPerMinute);
                }
                else
                {
                    ChartEvent ev = chartEvent as ChartEvent;
                    // Assumes that the sustain does not span a BPM change... probably good enough
                    ev.SustainSeconds = ev.Sustain * tickDelta;
                    maxFrame = (int)(timePointer / (1.0 / frameRate));
                }
            }

            float[,] result = new float[maxFrame, 7];
            // Set all frames as a non-note until they are written to something else
            for(int i = 0; i < maxFrame; i++)
            {
                result[i,0] = 1;
            }

            foreach (Event chartEvent in eventList.OrderBy(e => e.Tick))
            {
                if (chartEvent is ChartEvent)
                {
                    ChartEvent ev = chartEvent as ChartEvent;

                    // Shift all Green-Orange notes up by one, shift Open notes down to 6
                    int note = ev.Type;
                    int startFrame = (int)(chartEvent.AbsoluteTime / (1.0 / frameRate));
                    int endFrame = startFrame + (int)(chartEvent.AbsoluteTime + ev.SustainSeconds / (1.0 / frameRate));
                    while (startFrame <= endFrame && startFrame < maxFrame)
                    {
                        // zero out the label frame
                        for(int i = 0; i < 7; i++)
                        {
                            result[startFrame, i] = 0;
                        }

                        for(int i = 0; i < 8; i++)
                        {
                            if ((note & (1 << i)) > 0)
                            {
                                result[startFrame, Math.Min(i + 1, 6)] = 1;
                            }
                        }

                        break;
                        //startFrame++;
                    }
                }
            }

            return result;
        }
    }
}
