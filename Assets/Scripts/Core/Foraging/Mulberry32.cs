namespace Bush.Core
{
    public interface IRandom
    {
        /// <summary>A number in [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// Small seeded random generator. The web sandbox uses the exact same algorithm, so a seed
    /// gives the same expedition in both (see docs/FORAGING.md).
    /// </summary>
    public class Mulberry32 : IRandom
    {
        private uint _state;

        public Mulberry32(uint seed)
        {
            _state = seed;
        }

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                uint t = _state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
