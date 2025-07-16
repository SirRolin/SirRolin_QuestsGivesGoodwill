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
        public int extraGoodwillFlat = 5;
        public int maxGoodwillGain = 20;
        public int maxGoodwillLoss= 20;
        public bool canGoodwillBeNegative = true;
        public bool tooMuchGoodwillGivesSilver = true;

        //// Honour
        public bool honourIgnoresGoodwill = false;
        public float honourWorth = 665f;

        //// SpecificLootBehaivior
        public bool campLootIgnoresGoodwill = true;
        public int campLootProcentValue = 50;

        public bool enableMinLootValue = true;
        public float minLootValueProOfReward = 30f;
        public int extraLootTries = 3;
        public int extraLootMinWorthForTry = 1000;
        public bool enableSilverRemainder = true;

        public bool enableCleanupLogic = true;
        public int cleanupItemWeight = 0;

        //// Boost Rewards
        public bool boostRewards = true;
        public float boostRewardsProcentage = 20f;

        //// debugging
        public bool debuggingOverflow = false;
        public bool debuggingVerbose = false;

        public override void ExposeData()
        {
            //// Goodwill
            Scribe_Values.Look(ref goodwillWorth, "goodwillWorth", 100);
            Scribe_Values.Look(ref extraGoodwillPro, "extraGoodwillPro", 20f);
            Scribe_Values.Look(ref extraGoodwillFlat, "extraGoodwillFlat", 5);
            Scribe_Values.Look(ref maxGoodwillGain, "maxGoodwillGain", 20);
            Scribe_Values.Look(ref maxGoodwillLoss, "maxGoodwillLoss", 20);
            Scribe_Values.Look(ref canGoodwillBeNegative, "canGoodwillBeNegative", true);
            Scribe_Values.Look(ref tooMuchGoodwillGivesSilver, "tooMuchGoodwillGivesExtraLoot", true);

            //// Honour
            Scribe_Values.Look(ref honourIgnoresGoodwill, "honourIgnoresGoodwill", true);
            Scribe_Values.Look(ref honourWorth, "honourWorth", 665f);

            //// SpecificLootBehaivior
            Scribe_Values.Look(ref campLootIgnoresGoodwill, "campLootIgnoresGoodwill", true);
            Scribe_Values.Look(ref campLootProcentValue, "campLootProcentValue", 50);
            Scribe_Values.Look(ref enableMinLootValue, "enableMinLootValue", true);
            Scribe_Values.Look(ref minLootValueProOfReward, "minLootValueProOfReward", 30f);
            Scribe_Values.Look(ref extraLootTries, "extraLootTries", 3);
            Scribe_Values.Look(ref extraLootMinWorthForTry, "extraLootMinWorthForTry", 1000);
            Scribe_Values.Look(ref enableSilverRemainder, "enableSilverRemainder", true);

            Scribe_Values.Look(ref enableCleanupLogic, "enableCleanupLogic", true);
            Scribe_Values.Look(ref cleanupItemWeight, "cleanupItemWeight", 0);

            //// Boost Rewards
            Scribe_Values.Look(ref enableMinLootValue, "boostRewards", true);
            Scribe_Values.Look(ref minLootValueProOfReward, "boostRewardsProcentage", 20f);

            //// debugging
            Scribe_Values.Look(ref debuggingOverflow, "debuggingOverflow", false);
            Scribe_Values.Look(ref debuggingVerbose, "debuggingVerbose", false);

            base.ExposeData();
        }
    }
}
