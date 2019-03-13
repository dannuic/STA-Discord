using System;
using System.Collections.Generic;
using System.Linq;

namespace STA_Discord
{
    public class Dice
    {
        private static readonly Random random = new Random(); // this should really be random enough

        public static T Roll<T>(List<T> sides)
        {
            lock(random)
            {
                return sides.ElementAt(random.Next(0, sides.Count - 1));
            }
        }

        // convenience roll function for a standard n-sided die
        public static int Roll(int num_sides)
        {
            return Roll(Enumerable.Range(1, num_sides).ToList());
        }
    }
}
