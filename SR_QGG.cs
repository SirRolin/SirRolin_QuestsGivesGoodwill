using System.Collections.Generic;

using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using Multiplayer.API;
using System;
using System.Text;

namespace SirRolin.QuestsGiveGoodwill
{

    [StaticConstructorOnStartup]
    public class PatchMain
    {
        static PatchMain()
        {
            Harmony val = new Harmony("SR.QGG");
            val.PatchAll();
            if (MP.enabled)
            {
                MP.RegisterAll();
            }
        }
    }
    public class Goodwill_Settings : ModSettings
    {
        /// <summary>
        /// Goodwill
        /// </summary>
        public static float goodwillWorth = 100f;
        public static float extraGoodwillPro = 0.2f;
        public static int extraGoodwillFlat = 2;
        public static bool canGoodwillBeNegative = true;

        /// <summary>
        /// Honour
        /// </summary>
        public static bool honourIgnoresGoodwill = true;
        public static float honourWorth = 100f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref goodwillWorth, "goodwillWorth", 100f);
            Scribe_Values.Look(ref extraGoodwillPro, "extraGoodwillPro", 0.2f);
            Scribe_Values.Look(ref extraGoodwillFlat, "extraGoodwillFlat", 2);
            Scribe_Values.Look(ref canGoodwillBeNegative, "canGoodwillBeNegative");
            Scribe_Values.Look(ref honourWorth, "honourWorth", 100f);
            Scribe_Values.Look(ref honourIgnoresGoodwill, "honourIgnoresGoodwill");
            base.ExposeData();
        }
    }

    [HarmonyPatch(typeof(RewardsGenerator), "DoGenerate")]
    class QuestsGiveGoodwill : Mod
    {

        private string extraFlatGoodwillText = string.Empty;

        /// <summary>
        /// A mandatory constructor which resolves the reference to our settings.
        /// </summary>
        /// <param name="content"></param>
        public QuestsGiveGoodwill(ModContentPack content) : base(content)
        {
        }

        /// <summary>
        /// The (optional) GUI part to set your settings.
        /// </summary>
        /// <param name="inRect">A Unity Rect with the size of the settings window.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            //// Goodwill
            listingStandard.Label("Goodwill: ");
            listingStandard.CheckboxLabeled("Can goodwill be negative?", ref Goodwill_Settings.canGoodwillBeNegative, "Sometimes quest rewards are worth more than the quest is worth, I take this as the quest givers are desperate, but unhappy about the price.");
            listingStandard.Label("What's the worth of goodwill in silver? " + Goodwill_Settings.goodwillWorth);
            Goodwill_Settings.goodwillWorth = listingStandard.Slider(Goodwill_Settings.goodwillWorth, 50f, 500f);
            listingStandard.Label(Goodwill_Settings.extraGoodwillPro + "% extra wealth for goodwill. (negative in case you want to roleplay hostile world)");
            Goodwill_Settings.extraGoodwillPro = listingStandard.Slider(Goodwill_Settings.extraGoodwillPro, -1f, 2f);
            listingStandard.Label(Goodwill_Settings.extraGoodwillFlat + " flat extra goodwill on all quest rewards.");
            Goodwill_Settings.extraGoodwillFlat = listingStandard.TextFieldNumeric(ref Goodwill_Settings.extraGoodwillFlat, ref extraFlatGoodwillText, -10, 10);

            //// Honour
            listingStandard.Label("Honour: ");
            listingStandard.Label("When Honour is offered, should it Only be honour that's offered?");
            listingStandard.CheckboxLabeled("can goodwill be negative?", ref Goodwill_Settings.honourIgnoresGoodwill);
            Goodwill_Settings.honourWorth = listingStandard.Slider(Goodwill_Settings.honourWorth, 50f, 500f);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// Using .Translate() is optional, but does allow for localisation.
        /// </summary>
        /// <returns>The (translated) mod name.</returns>
        public override string SettingsCategory()
        {
            return "QuestsGiveGoodwill".Translate();
        }


        public static List<Reward> Postfix(List<Reward> __result, RewardsGeneratorParams parms)
        {
            if (parms.giverFaction != null && parms.giverFaction.CanEverGiveGoodwillRewards)
            {
                //// Initiate reward in silver value then remove for each item reward to get the missing amount.
                float unaccountedReward = parms.rewardValue * (1f + Goodwill_Settings.extraGoodwillPro) + (Goodwill_Settings.extraGoodwillFlat * Goodwill_Settings.goodwillWorth);
                int goodwillIndex = -1; //To check if there's already a goodwill for the faction.
                int counter = 0;
                foreach (Reward reward in __result)
                {

                    //// Find out if it's goodwill
                    if (reward is Reward_Goodwill goodwill)
                    {
                        if (goodwill.faction == parms.giverFaction)
                        {
                            goodwillIndex = counter;
                        }
                        else
                        {
                            //// Decrease reward by 100 times the goodwill
                            unaccountedReward -= goodwill.amount * Goodwill_Settings.goodwillWorth;
                        }
                    }
                    else if (reward is Reward_RoyalFavor favor)
                    {

                        //// if it's Favour ignore goodwill.
                        if (Goodwill_Settings.honourIgnoresGoodwill)
                        {
                            unaccountedReward = 0;
                            break;
                        }
                        else
                        {
                            //// Decrease reward by 200 times the favor
                            unaccountedReward -= favor.amount * Goodwill_Settings.honourWorth;
                        }
                    }
                    else if (reward is Reward_Pawn pawn)
                    {
                        //// Decrease reward by the value of the pawn - For Some Reason it's special
                        unaccountedReward -= pawn.pawn.MarketValue;
                    }
                    else
                    {
                        //// Decrease reward by item value
                        unaccountedReward -= reward.TotalMarketValue;
                    }

                    counter++;
                }

                //float worth;
                Reward_Goodwill goal = new Reward_Goodwill();
                //goal.InitFromValue(unaccountedReward, parms, out worth);
                //Log.Message(goal.faction + ":" + goal.amount + " worth " + worth + " (from " + unaccountedReward + ") via the InitFromValue.");
                goal.faction = parms.giverFaction;
                goal.amount = (int)(unaccountedReward / Goodwill_Settings.goodwillWorth);
                if (goal.faction.PlayerGoodwill - 100 > goal.amount)
                {
                    int needed = (100 - goal.faction.PlayerGoodwill);
                    goal.amount = needed;
                    //// Getting the silver value of the reward.
                    Reward_Items silver = new Reward_Items();
                    float worthOfItemGenerated;
                    silver.InitFromValue(unaccountedReward - (goal.amount * Goodwill_Settings.goodwillWorth), parms, out worthOfItemGenerated);
                    StringBuilder sb = new StringBuilder();
                    foreach (Thing s in silver.items)
                    {
                        sb.Append(s.ToString() + " - ");
                    }
                    Log.Message("Goodwill generated items: " + sb.ToString() + "Worth: " + worthOfItemGenerated + " - expected: " + silver.TotalMarketValue);
                }

                //// If there's already a goodwill
                if (goodwillIndex != -1)
                {
                    ((Reward_Goodwill)__result[goodwillIndex]).amount = goal.amount;
                }
                //// If there's no goodwill already
                else
                {
                    //// setup so I can introduce options.
                    if (goal.amount > 0 || (goal.amount < 0 && Goodwill_Settings.canGoodwillBeNegative))
                    {
                        __result.Add(goal);
                    }
                }
            }
            return __result;
        }
    }
}

