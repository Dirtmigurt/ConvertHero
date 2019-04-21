namespace ConvertHero.Models
{
    /// <summary>
    /// Base class for clone hero events.
    /// </summary>
    public class Event
    {
        /// <summary>
        /// The absolute time that the event occurs (2.354 seconds)
        /// </summary>
        public double AbsoluteTime;

        /// <summary>
        /// The tick that the event occurs on (480)
        /// </summary>
        public long Tick;
    }
}
