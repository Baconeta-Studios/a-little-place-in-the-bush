using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>One run of something that snaps end to end (fairy lights): its middle, and the distance between its two ends.</summary>
    public readonly struct ChainPiece
    {
        public readonly float x;
        public readonly float y;
        public readonly float span;

        public ChainPiece(float x, float y, float span)
        {
            this.x = x;
            this.y = y;
            this.span = span;
        }

        public float LeftEnd => x - span * 0.5f;
        public float RightEnd => x + span * 0.5f;
    }

    /// <summary>
    /// How pieces like fairy lights join: end to end, in a straight line, with one pole shared at each join.
    /// Nothing about a join is saved: two pieces are joined when their ends are in the same place, so moving
    /// one away simply parts them.
    /// </summary>
    public static class ChainLights
    {
        private const float SameSpot = 0.01f;

        /// <summary>Whether something else's end is at this piece's left and right ends.</summary>
        public static (bool left, bool right) Joins(ChainPiece piece, IEnumerable<ChainPiece> others)
        {
            bool left = false, right = false;
            foreach (ChainPiece other in others)
            {
                if (Math.Abs(other.y - piece.y) > SameSpot) continue;
                if (Math.Abs(other.RightEnd - piece.LeftEnd) <= SameSpot) left = true;
                if (Math.Abs(other.LeftEnd - piece.RightEnd) <= SameSpot) right = true;
            }
            return (left, right);
        }

        /// <summary>
        /// The nearest place within <paramref name="radius"/> where a piece of this span would join a free end
        /// of another piece. False if there's none.
        /// </summary>
        public static bool TrySnap(float span, float wantedX, float wantedY, IReadOnlyList<ChainPiece> others, float radius, out float x, out float y)
        {
            x = wantedX;
            y = wantedY;
            bool found = false;
            double best = radius;
            foreach (ChainPiece other in others)
            {
                float gap = (other.span + span) * 0.5f;
                (bool left, bool right) = Joins(other, others);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (side < 0 ? left : right) continue; // that end's taken
                    float spotX = other.x + side * gap;
                    double distance = Math.Sqrt((spotX - wantedX) * (spotX - wantedX) + (other.y - wantedY) * (other.y - wantedY));
                    if (distance > best) continue;
                    best = distance;
                    found = true;
                    x = spotX;
                    y = other.y;
                }
            }
            return found;
        }
    }
}
