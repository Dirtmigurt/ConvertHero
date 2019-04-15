using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.Models
{
    public class SyncEvent : Event
    {
        public double BeatsPerMinute = -1;

        public int Numerator = -1;

        public int Denominator = -1;

        /// <summary>
        /// Create a sync event for a new tempo
        /// </summary>
        /// <param name="tick">
        /// Tick where this sync event occurs
        /// </param>
        /// <param name="microsecondsPerQuarterNote">
        /// Number of microseconds each quarter note gets in this new tempo.
        /// </param>
        public SyncEvent(long tick, double beatsPerMinute)
        {
            this.Tick = tick;
            this.BeatsPerMinute = beatsPerMinute;
        }

        /// <summary>
        /// Create a sync event for a new time signature
        /// </summary>
        /// <param name="tick">
        /// Tick where this sync event occurs
        /// </param>
        /// <param name="numerator">
        /// Numerator in the time signatue fraction i.e. 3 in 3/4
        /// </param>
        /// <param name="denominator">
        /// Denominator in the time signature fraction i.e. 4 in 3/4
        /// </param>
        public SyncEvent(long tick, int numerator, int denominator)
        {
            this.Tick = tick;
            this.Numerator = numerator;
            this.Denominator = denominator;
        }

        public override string ToString()
        {
            if (this.BeatsPerMinute < 0)
            {
                // This is a time signature event
                if (this.Denominator == 4)
                {
                    return $"  {this.Tick} = TS {this.Numerator}";
                }
                else
                {
                    int denom = (int)(Math.Log(this.Denominator) / Math.Log(2));
                    return $"  {this.Tick} = TS {this.Numerator} {denom}";
                }
            }
            else
            {
                return $"  {this.Tick} = B {(int)(this.BeatsPerMinute * 1000)}";
            }
        }

        public string ToEventString()
        {
            return $"  {this.Tick} = E \"section BPM TO {this.BeatsPerMinute}\"";
        }
    }
}
