using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SirRolin.QuestsGiveGoodwill
{
    public class Goodwill_Settings : ModSettings
    {
        //// Goodwill
        public float goodwillWorth = 100;
        public float extraGoodwillPro = 20f;
        public int extraGoodwillFlat = 2;
        public bool canGoodwillBeNegative = true;

        //// Honour
        public bool honourIgnoresGoodwill = true;
        public float honourWorth = 100;


        //// debugging
        public bool debuggingOverflow = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref goodwillWorth, "goodwillWorth", 100);
            Scribe_Values.Look(ref extraGoodwillPro, "extraGoodwillPro", 20f);
            Scribe_Values.Look(ref extraGoodwillFlat, "extraGoodwillFlat", 2);
            Scribe_Values.Look(ref canGoodwillBeNegative, "canGoodwillBeNegative");
            Scribe_Values.Look(ref honourWorth, "honourWorth", 100);
            Scribe_Values.Look(ref honourIgnoresGoodwill, "honourIgnoresGoodwill");
            Scribe_Values.Look(ref debuggingOverflow, "debuggingOverflow");
            base.ExposeData();
        }
    }
}
