using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SirRolin.QuestsGiveGoodwill.HelpingFunctions
{
    internal class Randomness
    {
        /// <summary>
        /// skews a value using a userfriendly skew value.
        /// Lower skews more towards 0
        /// Higher skews more towards 1
        /// </summary>
        /// <param name="value">float between 0 and 1</param>
        /// <param name="inputSkew">User friendly Skew</param>
        /// <param name="minSkew">Lowest the User Friendly skew goes</param>
        /// <param name="maxSkew">Highest the User Friendly skew goes</param>
        /// <param name="skewAmount">How much exponentially the skew influences the value</param>
        /// <returns></returns>
        public static float SkewValue(float value, float inputSkew, float minSkew, float maxSkew, float skewAmount = 2f)
        {
            // random value must be between 0 and 1 to make sense
            value = Mathf.Clamp01(value);

            // must be higher than 1 to make sense
            skewAmount = Mathf.Max(1, skewAmount);

            float exp = 1f + (skewAmount - 1f) *  Mathf.InverseLerp(minSkew, maxSkew, inputSkew);

            if (inputSkew < 0)
                return Mathf.Pow(value, exp);       // Skew toward 0
            else if (inputSkew > 0)
                return 1f - Mathf.Pow(1f - value, exp); // Skew toward 1
            else
                return value; // No skew
        }

    }
}
