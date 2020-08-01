namespace ConvertHero.Models
{
    using System;

    /// <summary>
    /// ChartEvent contains all of the information about a note in a clone hero track.
    /// </summary>
    public class ChartEvent : Event
    {
        /// <summary>
        /// The type of the event.
        /// </summary>
        public int Type = 0;

        /// <summary>
        /// The real instruments tone for this event.
        /// </summary>
        public int Tone = 0;

        /// <summary>
        /// The number of ticks this note should be held for.
        /// </summary>
        public long Sustain = -1;

        /// <summary>
        /// Chord indicators the number of notes that land on the same tick (for a guitar a note=1, chords=2+)
        /// </summary>
        public int Chord = 1;

        /// <summary>
        /// The number of tones that a chord event spans. If a chord is C2+C3, then the span = 1 octave = 12 tones.
        /// </summary>
        public int ToneSpan = 0;

        /// <summary>
        /// This indicates whether or not the note is played on the cymals on a drum track.
        /// </summary>
        public bool IsCymbal = false;

        /// <summary>
        /// Constructor for the ChartEvent class.
        /// </summary>
        /// <param name="tick">
        /// The tick that this note falls on.
        /// </param>
        /// <param name="type">
        /// The type of this note.
        /// </param>
        /// <param name="sustain">
        /// how long the note is held for.
        /// </param>
        /// <param name="chord">
        /// The number of ChartEvents that fall on the same tick.
        /// </param>
        /// <param name="span">
        /// The span of tones that fall on the same tick.
        /// </param>
        public ChartEvent(long tick, int type, long sustain = 0, int chord = 1, int span = 0)
        {
            this.Tick = tick;
            this.Type = type;
            this.Tone = type;
            this.Sustain = sustain;
            this.Chord = chord;
            this.ToneSpan = span;
        }

        /// <summary>
        /// A safe parsing method used to convert a string formatted by Moonscraper to a ChartEvent.
        /// </summary>
        /// <param name="line">
        /// The string containing all of the event information. Of the format "1920 = N 3 0"
        /// "<Tick> = N <Type> <Sustain>"
        /// </param>
        /// <param name="chartEvent">
        /// The chartEvent that is created from the input string.
        /// </param>
        /// <returns>
        /// True if the chart event was successfully created.
        /// </returns>
        public static bool TryParse(string line, out ChartEvent chartEvent)
        {
            chartEvent = null;
            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5)
            {
                return false;
            }

            if (tokens[2] != "N")
            {
                return false;
            }

            if (!int.TryParse(tokens[0], out int tick))
            {
                return false;
            }

            if (!int.TryParse(tokens[3], out int type))
            {
                return false;
            }

            if (!int.TryParse(tokens[4], out int sustain))
            {
                return false;
            }

            chartEvent = new ChartEvent(tick, type, sustain);
            return true;
        }

        /// <summary>
        /// Convert the ChartEvent to a Moonscraper friendly line.
        /// </summary>
        /// <returns>
        /// String of the format "<Tick> = N <Type> <Sustain>"
        /// </returns>
        public override string ToString()
        {
            string strRepresentation = $"  {this.Tick} = N {this.Type} {this.Sustain}";
            if (this.IsCymbal)
            {
                strRepresentation += $"\n  {this.Tick} = N {64 + this.Type} {this.Sustain}";
            }

            return strRepresentation;
        }
    }

    /// <summary>
    /// Enum containing all possible Types that Moonscraper supports
    /// </summary>
    public enum NoteType
    {
        Green,
        Red,
        Yellow,
        Blue,
        Orange,
        Forced,
        Tap,
        Open,
        Unknown
    }

    /// <summary>
    /// Enum containing all of the Drum Note types
    /// </summary>
    public enum DrumType
    {
        Kick,
        Snare,
        HiHat,
        Crash,
        Ride,
        Tom,
        Misc
    }

    /// <summary>
    /// Enum containing all of the supported Clone Hero instruments.
    /// </summary>
    public enum CloneHeroInstrument
    {
        Single,
        DoubleBass,
        Drums
    }

    /// <summary>
    /// Enum containing all of the supported Clone Hero difficulties.
    /// </summary>
    public enum CloneHeroDifficulty
    {
        Expert,
        Hard,
        Medium,
        Easy
    }
}
