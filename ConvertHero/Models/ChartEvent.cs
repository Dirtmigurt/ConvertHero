using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.Models
{
    public class ChartEvent : Event
    {
        // Type of event, defined by the enum NoteType
        public int Type = 0;

        public int Tone = 0;

        // Number of ticks this Note is held for
        public long Sustain = -1;

        // Chord indicators the number of notes that land on the same tick (for a guitar a note=1, chords=2+)
        public int Chord = 1;

        public int ToneSpan = 0;

        public ChartEvent(long tick, int type, long sustain = 0, int chord = 1, int span = 0)
        {
            this.Tick = tick;
            this.Type = type;
            this.Tone = type;
            this.Sustain = sustain;
            this.Chord = 1;
            this.ToneSpan = span;
        }

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

            int tick;
            if (!int.TryParse(tokens[0], out tick))
            {
                return false;
            }

            int type;
            if (!int.TryParse(tokens[3], out type))
            {
                return false;
            }

            int sustain;
            if (!int.TryParse(tokens[4], out sustain))
            {
                return false;
            }

            chartEvent = new ChartEvent(tick, type, sustain);
            return true;
        }

        public override string ToString()
        {
            return $"  {this.Tick} = N {this.Type} {this.Sustain}";
        }
    }

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

    public enum DrumType
    {
        Kick,
        Red,
        Yellow,
        Blue,
        Orange,
        Green
    }

    public enum CloneHeroInstrument
    {
        Single,
        DoubleBass,
        Drums
    }

    public enum CloneHeroDifficulty
    {
        Expert,
        Hard,
        Medium,
        Easy
    }
}
