using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Verse;
using Verse.Noise;

namespace SirRolin.QuestsGiveGoodwill
{
    class QuestsGiveGoodwill : Mod
    {
        public Goodwill_Settings settings;
        /// <summary>
        /// A mandatory constructor which resolves the reference to our settings.
        /// </summary>
        /// <param name="content"></param>
        public QuestsGiveGoodwill(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<Goodwill_Settings>();
        }

        /// <summary>
        /// Settings Strings, can't be from a list, but can from an array or loose variables.
        /// </summary>
        private string  maxGoodwillLossText;
        private string maxGoodwillGainText;
        private string goodwillWorthText;
        private string extraProGoodwillText;
        private string extraFlatGoodwillText;
        private string extraLootTriesText;
        private string extraLootMinWorthText;
        private string discardExtraWeightText;

        private string honourWorthText;
        private string camplootProWorthText;
        private string minLootRewardText;
        private string boostRewardsProText;

        private Vector2 scrollPositions = new Vector2(0f, 0f);

        private const float ScrollBarWidthMargin = 20f;
        private float totalContentHeight = 10000f; //// corrected on every frame.
        private float goodwillSectionHeight = 420f;
        private float honourSectionHeight = 124f;
        private float itemSectionHeight = 520f;

        private int resetClicks = 0;

        /// <summary>
        /// The (optional) GUI part to set your settings.
        /// </summary>
        /// <param name="inRect">A Unity Rect with the size of the settings window.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            //// Create Scrollable Window.
            Rect outerRect = inRect.ContractedBy(10f);
            bool scrollBarVisible = totalContentHeight > outerRect.height;
            if (scrollBarVisible)
                inRect.width = inRect.width - ScrollBarWidthMargin;
            Rect scrollViewTotal = new Rect(inRect.x, inRect.y, inRect.width - (scrollBarVisible ? ScrollBarWidthMargin : 0), totalContentHeight);
            Widgets.BeginScrollView(outerRect, ref scrollPositions, scrollViewTotal);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(scrollViewTotal);

            //// Giving an Example
            listingStandard.Label("With current settings the average reward boosting will be like this:");
            float exampleReward = 1000f;
            float exampleRewardAfter = exampleReward * (1f + (settings.boostRewards ? (settings.boostRewardsProcentage / 100f) : 0f));
            float exampleGoodwillGain = exampleRewardAfter * (settings.extraGoodwillPro / 100f) / settings.goodwillWorth + settings.extraGoodwillFlat;
            listingStandard.Label(
                String.Format("{0:N}$ -> {1:N2}$ plus {2:N2} Goodwill.",
                    exampleReward,
                    exampleRewardAfter,
                    Math.Round(exampleGoodwillGain, 2)
                    ));

            listingStandard.Label("With current settings the rough edgecasing would be like this:");
            float minExampleReward = Mathf.Min(exampleRewardAfter * (1-Mathf.Pow(0.5f, settings.extraLootTries)), exampleRewardAfter - settings.extraLootMinWorthForTry);
            listingStandard.Label(
                String.Format("{0:N} to {1:N2}$ -> {2:N2} to {3:N2}$ plus {4:N2} to {5:N2} Goodwill.",
                    Math.Round(exampleReward * 0.5f, 2),
                    Math.Round(exampleReward * 1.5f, 2),
                    Math.Round(minExampleReward, 2),
                    Math.Round(exampleRewardAfter * 1.5f),
                    Math.Round(((exampleRewardAfter - minExampleReward) / settings.goodwillWorth) + settings.extraGoodwillFlat, 2),
                    Math.Round((exampleGoodwillGain - (exampleGoodwillGain * 1.5f)) / settings.goodwillWorth + settings.extraGoodwillFlat, 2)
                    ));

            listingStandard.GapLine();

            if (listingStandard.ButtonText(resetClicks == 0 ? "Reset All" : resetClicks < 5 ? "You Sure you want to reset? (" + resetClicks + " out of 5)" : "Last Chance, Reset?"))
            {
                resetClicks++;
                if (resetClicks > 5)
                {
                    ResetAll();
                }
            }

            //// Goodwill
            listingStandard.Label("Goodwill: ");
            Listing_Standard goodwillSection = listingStandard.BeginSection(goodwillSectionHeight, bottomBorder: 0);
            goodwillSection.CheckboxLabeled("Enable Negative Goodwill Rewards (default Yes)", ref settings.canGoodwillBeNegative, "Sometimes quest rewards are worth more than the quest is worth, I take this as the quest givers are desperate, but unhappy about the price.");

            if (settings.canGoodwillBeNegative)
            {

                CreateSliderPlusTextField(goodwillSection,
                    "Max negative goodwill (Default 20)",
                    ref maxGoodwillLossText,
                    ref settings.maxGoodwillLoss,
                    min: 0, max: 100,
                    steps: 1);
            }

            CreateSliderPlusTextField(goodwillSection,
                "Max goodwill gain (Default 20)",
                ref maxGoodwillGainText,
                ref settings.maxGoodwillGain,
                min: 0, max: 100,
                hover: "Overflow generates more items instead.");

            CreateSliderPlusTextField(goodwillSection,
                "The worth of Goodwill in " + RimWorld.ThingDefOf.Silver.defName + "? " + settings.goodwillWorth + "$ (Default 100$)",
                ref goodwillWorthText,
                ref settings.goodwillWorth,
                min: 50f, max: 500f, steps: 0.5f);

            CreateSliderPlusTextField(goodwillSection,
                settings.extraGoodwillPro + "% extra wealth for goodwill. (negative in case you want to roleplay hostile world) (Default 20%)",
                ref extraProGoodwillText,
                ref settings.extraGoodwillPro,
                min: -100f, max: 200f, steps: 0.5f);

            CreateSliderPlusTextField(goodwillSection,
                settings.extraGoodwillFlat + " flat extra goodwill on all quest rewards. (Default 5)",
                ref extraFlatGoodwillText,
                ref settings.extraGoodwillFlat,
                min: -20, max: 20);

            goodwillSection.CheckboxLabeled("Enable Goodwill Overflow to silver (default Yes)", ref settings.tooMuchGoodwillGivesSilver, "If goodwill caps for some reason are met, it will give silve for the loss.");


            listingStandard.EndSection(goodwillSection);
            listingStandard.Gap();


            //// Honour
            listingStandard.Label("Honour: ");
            Listing_Standard honourSection = listingStandard.BeginSection(honourSectionHeight, bottomBorder: 0);
            honourSection.Label("When Honour is a reward, should I Skip it? (Default No)");
            honourSection.CheckboxLabeled(settings.honourIgnoresGoodwill ? "Honour is generated only with vanilla quests" : "Honour has a silver value and can be rewarded.", ref settings.honourIgnoresGoodwill);
            if (!settings.honourIgnoresGoodwill)
                CreateSliderPlusTextField(honourSection,
                    "The worth of Honour in items? (Default 665, vanilla 2000/3) " + settings.honourWorth + "$",
                    ref honourWorthText,
                    ref settings.honourWorth,
                    min: 50, max: 1000,
                    steps: 5);

            listingStandard.EndSection(honourSection);

            listingStandard.Gap();
            //// Items
            listingStandard.Label("Items: ");
            Listing_Standard otherSection = listingStandard.BeginSection(itemSectionHeight, bottomBorder: 0);
            otherSection.CheckboxLabeled("When Camp loot is a reward, should I ignore goodwill? (Default yes)", ref settings.campLootIgnoresGoodwill);

            if (!settings.campLootIgnoresGoodwill)
                CreateSliderPlusTextField(otherSection,
                    "Camp Loot % worth of reward.",
                    ref camplootProWorthText,
                    ref settings.campLootProcentValue,
                    min: -100, max: 100);

            otherSection.CheckboxLabeled("Enable Minimum Item Reward patch? (Default yes)", ref settings.enableMinLootValue);

            if (settings.enableMinLootValue)
                CreateSliderPlusTextField(otherSection,
                    "min % of reward allowed to be generated. (Vanilla: ~13%. Default 30%)",
                    ref minLootRewardText,
                    ref settings.minLootValueProOfReward,
                    min: 0f, max: 80f, steps: 0.5f);


            otherSection.CheckboxLabeled("Enable Extra Reward patch? (Default yes)", ref settings.boostRewards);

            if (settings.boostRewards)
            {
                CreateSliderPlusTextField(otherSection,
                    "Boost rewards by a %. (Default 30%)",
                    ref boostRewardsProText,
                    ref settings.boostRewardsProcentage,
                    min: -100f, max: 500f, steps: 0.5f);
            }

            CreateSliderPlusTextField(otherSection,
                settings.extraLootTries + " item generation cycles. (Vanilla 1, Default 3)",
                ref extraLootTriesText,
                ref settings.extraLootTries,
                min: 1, max: 8);
                
            CreateSliderPlusTextField(otherSection,
                settings.extraLootMinWorthForTry + "$ minimally needed for additional loot generation tries. (default 1000) - Setting this low and Item generation cycles high, will often result in more value, but less goodwill.",
                ref extraLootMinWorthText,
                ref settings.extraLootMinWorthForTry,
                min: 500, max: 5500, steps: 5);

            otherSection.CheckboxLabeled("Give Silver for the remainder of the reward? (Default yes)", ref settings.enableSilverRemainder);

            CreateSliderPlusTextField(otherSection,
                "Discard overestimated loot weigth - When too much loot is generated, negative priorities keeping high cost items, while positive numbers prioritieses keeping low cost items.\nDefault 0.",
                ref discardExtraWeightText,
                ref settings.cleanupItemWeight,
                min: -100, max: 100, steps: 1
                );

            listingStandard.EndSection(otherSection);


            //// Debugging
            if (Prefs.DevMode)
            {
                listingStandard.Gap();
                listingStandard.Label("Developer Menu");
                Listing_Standard devSec = listingStandard.BeginSection(48f, bottomBorder: 0);
                devSec.CheckboxLabeled("Debugging Overflow", ref settings.debuggingOverflow, "(Default No)");
                devSec.CheckboxLabeled("Debugging Parameters", ref settings.debuggingVerbose, "(Default No)");
                listingStandard.EndSection(devSec);
            }

            //// correcting the size of the window.
            if (totalContentHeight == 10000f)
                totalContentHeight = listingStandard.CurHeight;

            //// finishing the views (to avoid errors)
            listingStandard.End();
            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Creates a label to explain the setting, a text field and a slider which set the same setting
        /// </summary>
        /// <param name="ls">listing Standard to insert the ui</param>
        /// <param name="label">explanation text</param>
        /// <param name="textReference">a reference to a string that should be stored in the class (but not in a list)</param>
        /// <param name="settingRef">reference to setting value</param>
        /// <param name="min">lowest value</param>
        /// <param name="max">highest value</param>
        /// <param name="degits">rounding</param>
        /// <param name="hover">tooltip when hovering</param>
        private void CreateSliderPlusTextField(Listing_Standard ls, string label, ref string textReference, ref float settingRef, float min, float max, int degits = 1, string hover = null, float steps = 1f)
        {
            if (degits < 0) degits = 0;
            TaggedString taglabel = new TaggedString(label);
            ls.Label(taglabel, -1f, tooltip: hover);
            ls.TextFieldNumeric(ref settingRef, ref textReference, min, max);
            textReference = String.Format("{0:N" + degits + "}", RoundToNearest(ls.Slider(settingRef, min, max), steps));
        }

        private void CreateSliderPlusTextField(Listing_Standard ls, string label, ref string textReference, ref int settingRef, int min, int max, string hover = "", int steps = 1)
        {
            TaggedString taglabel = new TaggedString(label);
            ls.Label(taglabel, -1f, hover);
            ls.TextFieldNumeric(ref settingRef, ref textReference, min, max);
            textReference = ((int) RoundToNearest(ls.Slider(settingRef, min, max), steps)).ToString();
        }

        private double RoundToNearest(double number, double multiple)
        {
            if (multiple != 0)
            {
                return Math.Round(number / multiple) * multiple;
            }
            else
            {
                return Math.Round(number);
            }
        }

        private void ResetAll()
        {
            //// Goodwill
            maxGoodwillLossText = "20";
            goodwillWorthText = "100.0";
            extraProGoodwillText = "20.0";
            extraFlatGoodwillText = "5";
            maxGoodwillGainText = "20";
            settings.canGoodwillBeNegative = true;

            //// Honour
            settings.honourIgnoresGoodwill = false;
            honourWorthText = "665";

            //// SpecificLootBehaivior
            settings.campLootIgnoresGoodwill = true;
            camplootProWorthText = "50";

            settings.enableMinLootValue = true;
            minLootRewardText = "30.0";
            extraLootTriesText = "3";
            extraLootMinWorthText = "1000";
            settings.enableSilverRemainder = true;

            discardExtraWeightText = "0";


            //// Boost Rewards
            settings.boostRewards = true;
            boostRewardsProText = "20.0";

            //// debugging
            settings.debuggingOverflow = false;
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
    }
}
