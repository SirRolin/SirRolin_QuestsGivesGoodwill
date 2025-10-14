using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using SirRolin.QuestsGiveGoodwill.HelpingFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace SirRolin.QuestsGiveGoodwill.HarmonyPatches
{
    [HarmonyPatch(typeof(RewardsGenerator), "DoGenerate")]
    public static class GoodwillGiver
    {
        private const int MAXHONOUR = 12; /*Max Honour TO CONFIG*/
        public static System.Random rng = null;
        private static Goodwill_Settings settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();

        private static void MyMPCompat(){
            if (rng == null)
            {
                System.Random compatRng = new System.Random();
                if (MP.IsInMultiplayer)
                {
                    MP.WatchBegin();
                    MP.Watch(compatRng, nameof(compatRng));
                }
                rng = compatRng;
                if (MP.IsInMultiplayer)
                {
                    MP.WatchEnd();
                }
            }
        }

        private static Double GetRngDouble()
        {
            MyMPCompat();
            return rng.NextDouble();
        }

        private static int GetRangeRng(int min, int max)
        {
            MyMPCompat();
            return rng.Next(min,max);
        }

        public static void Prefix(ref RewardsGeneratorParams parms)
        {
            settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();
            if (settings.boostRewards)
                parms.rewardValue = parms.rewardValue * (1f + (settings.boostRewardsProcentage / 100f));
            if (settings.enableMinLootValue)
                parms.minGeneratedRewardValue = parms.rewardValue * (settings.minLootValueProOfReward / 100f);
        }


        public static List<Reward> Postfix(List<Reward> __result, RewardsGeneratorParams parms, out float generatedRewardValue)
        {
            settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();

            //// Debugging
            if (settings.debuggingVerbose)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"_result: {PrintRewardList(__result, parms)}\nparms: {parms}");
                if (sb.Length > 0)
                    Log.Message(sb.ToString());
            }

            bool flagIsGoodwillOrFavor = __result.Count==1 && (__result[0] is Reward_Goodwill || __result[0] is Reward_RoyalFavor rh);

            if (!parms.thingRewardDisallowed & parms.giverFaction != null || flagIsGoodwillOrFavor)
            {
                float missingValue = parms.rewardValue;                

                //// Calculate the amount of value that's missing and getting the index of any goodwill while at it.
                int goodwillIndex = CalculateWorthofList(__result, parms, settings, ref missingValue);

                //// Try to Aim for this amount of Goodwill
                int goodwillToAimFor = 0;

                if (goodwillIndex != -1)
                {
                    ///min-max is a clamp, due to vanilla no caring about my config settings when it comes to giving goodwill. :P
                    goodwillToAimFor =  Math.Min(settings.maxGoodwillCap - parms.giverFaction.PlayerGoodwill,
                                        Math.Min(settings.maxGoodwillGain,
                                        Math.Max(-settings.maxGoodwillLoss,
                                        ((Reward_Goodwill)__result[goodwillIndex]).amount)));

                    missingValue += (((Reward_Goodwill)__result[goodwillIndex]).amount - goodwillToAimFor) * settings.goodwillWorth;

                    ((Reward_Goodwill)__result[goodwillIndex]).amount = goodwillToAimFor;
                }
                else if (parms.giverFaction.allowGoodwillRewards) // if the faction can't give goodwill don't aim for any. (example permanent hostile factions)
                {
                    /// for the future for customisability of weight between greedy players (Negative) and generous players (Positive) | 20 = generosity | -100 max Greed | 100 max Generosity
                    float rng = (float)GetRngDouble();
                    float t = Randomness.SkewValue(rng, 20, -100, 100);

                    int maxGoodwillLoss = settings.canGoodwillBeNegative ? -settings.maxGoodwillLoss : 0;
                    goodwillToAimFor = Mathf.RoundToInt(Mathf.Lerp(maxGoodwillLoss, settings.maxGoodwillGain, t));

                    goodwillToAimFor = Math.Min(settings.maxGoodwillCap - parms.giverFaction.PlayerGoodwill, goodwillToAimFor);

                    //// Debugging
                    if (settings.debuggingVerbose)
                    {
                        Log.Message($"Aimed for Goodwill: {goodwillToAimFor} due to Rng skew: rng({rng}): skewed ({t})");
                    }

                    missingValue -= (goodwillToAimFor * settings.goodwillWorth);
                }


                AdjustHonour(__result, ref missingValue);

                //// Generate new items before goodwill rewards are added.
                TryGeneratingNewRewards(__result, parms, ref missingValue);

                if (settings.tooMuchGoodwillGivesSilver)
                    EnsureRewardWorth(__result, parms, settings, ref missingValue);

                float goodwillWorthToAdd = missingValue + parms.rewardValue * (settings.extraGoodwillPro / 100f) + ((goodwillToAimFor + settings.extraGoodwillFlat) * settings.goodwillWorth);

                if (parms.giverFaction != null && parms.giverFaction.CanEverGiveGoodwillRewards && !parms.thingRewardItemsOnly)
                {
                    //// Initiate reward in items value then remove for each item reward to get the missing amount.
                    Reward_Goodwill goodwillReward = new Reward_Goodwill();
                    goodwillReward.faction = parms.giverFaction;

                    //// Get the lowest of "missing goodwill for 100" & "max goodwill gain" & highest of -"Max Loss" & "Available Goodwill for the Amount"
                    int goodwill = Math.Min(settings.maxGoodwillCap - goodwillReward.faction.PlayerGoodwill,
                        Math.Min((parms.thingRewardDisallowed ? settings.maxGoodwillCap : settings.maxGoodwillGain), // If it only allows goodwill reward, limit it to max, otherwise limit it to settings.
                        Math.Max(-settings.maxGoodwillLoss,
                        (int)Math.Ceiling(goodwillWorthToAdd / settings.goodwillWorth))));

                    goodwillReward.amount += goodwill;

                    //// Then account for the reward.
                    missingValue -= goodwillReward.amount * settings.goodwillWorth;

                    //// finally we add the goodwill.
                    if (goodwillReward.amount > 0 || (goodwillReward.amount < 0 && settings.canGoodwillBeNegative))
                    {
                        //// If there's already a goodwill
                        if (goodwillIndex != -1)
                        {
                            ((Reward_Goodwill)__result[goodwillIndex]).amount = goodwillReward.amount;
                        }
                        //// If there's no goodwill already
                        else
                        {
                            __result.Add(goodwillReward);
                        }
                    } //// if it's 0 (or negative but not allowed) and the quest normally provided Goodwill
                    else if (goodwillReward.amount <= 0 && goodwillIndex != -1)
                    {
                        __result.Remove(__result[goodwillIndex]);
                        goodwillIndex = -1;
                    }
                }
                //// If there's no faction, there can be no goodwil or honour
                else if (parms.giverFaction == null)
                {
                    //// Generate new items before goodwill rewards are added.
                    TryGeneratingNewRewards(__result, parms, ref missingValue);

                    if (settings.tooMuchGoodwillGivesSilver)
                        EnsureRewardWorth(__result, parms, settings, ref missingValue);
                }

                generatedRewardValue = goodwillWorthToAdd - missingValue;

                /// I have a suspicioun that if there's goodwill in the rewards the loot is disbanded.
                parms.allowGoodwill = true;

                /// I know that if there's Honour in a reward and it wasn't a vanilla Honour Reward it gets disgarded.
                if (!settings.honourIgnoresGoodwill && parms.giverFaction.allowRoyalFavorRewards)
                {
                    parms.allowRoyalFavor = true;
                }

                /// I know that if there's Items in a reward and it wasn't allowed originally it gets disgarded.
                parms.thingRewardDisallowed = false;

                /// ?
                parms.allowDevelopmentPoints = true;
            }
            else
            {
                generatedRewardValue = 0;
            }


            return __result;
        }

        private static String PrintRewardList(List<Reward> rewards, RewardsGeneratorParams parms)
        {
            StringBuilder sb = new StringBuilder();
            float sumValue = 0;
            foreach (var reward in rewards)
            {
                sumValue += CalculateWorth(reward, settings, parms);
            }
            sb.AppendLine($"total: {sumValue}");
            foreach (var item in rewards)
            {
                if (item is Reward_Items ri)
                {
                    foreach (var item1 in ri.items)
                    {
                        sb.AppendLine($"{item1.stackCount}x {item1.GetCustomLabelNoCount()} ({item1.MarketValue} * {item1.stackCount} = {item1.MarketValue * item1.stackCount})");
                    }
                }
                else if (item is Reward_Pawn rp)
                {
                    sb.AppendLine("Pawn: " + rp.pawn.Name + " (" + rp.pawn.MarketValue + ")");
                }
                else if (item is Reward_Goodwill rg)
                {
                    sb.AppendLine($"Goodwill: ({rg.amount} * {settings.goodwillWorth} = {rg.amount * settings.goodwillWorth})");
                }
                else if (item is Reward_RoyalFavor rh)
                {
                    sb.AppendLine($"Honour: ({rh.amount} * {settings.honourWorth} = {rh.amount * settings.honourWorth})");
                }
                else
                {
                    sb.AppendLine($"Other: {item.GetType().Name} ({item.TotalMarketValue})");
                }
            }
            return sb.ToString();
        }

        private static float TryGeneratingNewRewards(List<Reward> __result, RewardsGeneratorParams parms, ref float missingValue)
        {
            if (missingValue > settings.extraLootMinWorthForTry)
            {
                int honourIndex = TypeIndexInList(__result, typeof(Reward_RoyalFavor));

                /// If they are not allowed go figure, we can't generate lot and it throws an error.
                bool originalThingsDisallowed = parms.thingRewardDisallowed;
                parms.thingRewardDisallowed = false;

                bool originalThingsRequired = parms.thingRewardRequired;
                parms.thingRewardRequired = true;

                //// Try to generate items, upto the settings amount.
                for (int i = 1; i < settings.extraLootTries; i++)
                {
                    // TO DO make settings - Idea a list with slides to set the chance of individual types of rewards.
                    if (Faction.OfEmpire != null /// Can only be rewarded to empire factions (from what I know)
                        && (honourIndex == -1 || ((Reward_RoyalFavor) __result[honourIndex]).amount < MAXHONOUR /*Max Honour TO CONFIG*/) /// if we haven't hit the limit already
                        && GetRngDouble() <  0.15f/*Favour RNG TO CONFIG*/) // random chance
                    {
                        int honourAmount = GetRangeRng(1, (int)(missingValue / settings.honourWorth));
                        if (honourAmount > MAXHONOUR)
                        {
                            honourAmount = MAXHONOUR;
                        }

                        if (honourIndex == -1) { 
                            Reward_RoyalFavor rf = new Reward_RoyalFavor
                            {
                                amount = honourAmount,
                                faction = Faction.OfEmpire
                            };
                            honourIndex = __result.Count;
                            __result.Add(rf);
                            missingValue -= rf.amount * settings.honourWorth;
                        } else if(__result[honourIndex] is Reward_RoyalFavor reward_RoyalFavor)
                        {
                            int was = reward_RoyalFavor.amount;
                            reward_RoyalFavor.amount = honourAmount;
                            missingValue -= (honourAmount - was) * settings.honourWorth;
                        }
                    }
                    else
                    {
                        GenerateNewItems(__result, parms, settings, ref missingValue);
                    }
                    if (missingValue <= settings.extraLootMinWorthForTry)
                    {
                        break;
                    }
                }

                parms.thingRewardDisallowed = originalThingsDisallowed;
                parms.thingRewardRequired = originalThingsRequired;
            }

            // Merge multiple stacks of the same item into 1
            foreach (var item in __result)
            {
                if (item is Reward_Items ri)
                {
                    for (int i = ri.items.Count - 1; i > 0; i--)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (ri.items[i].CanStackWith(ri.items[j]))
                            {
                                ri.items[j].stackCount += ri.items[i].stackCount;
                                ri.items.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            // Cleanup if enabled
            if (settings.enableCleanupLogic)
                ReduceItems(__result, ref missingValue);


            return missingValue;
        }

        private static void AdjustHonour(List<Reward> rewards, ref float missingValue)
        {
            if (rewards.Count == 0) return;
            if (settings.honourIgnoresGoodwill) return;
            foreach (var reward in rewards)
            {
                if (reward is Reward_RoyalFavor reward_Honour)
                {
                    int honour = Math.Min(1,
                                 Math.Max(12,
                                 Mathf.FloorToInt(reward_Honour.amount + (-missingValue / settings.honourWorth))));
                    int initial = reward_Honour.amount;
                    float initialMissingValue = missingValue;
                    missingValue -= (honour - reward_Honour.amount) * settings.honourWorth;
                    reward_Honour.amount = honour;

                    if (settings.debuggingVerbose)
                    {
                        Log.Message($"Honour amount changed from {initial} to {honour}, changing missing value from {initialMissingValue} to {missingValue}");
                    }
                }
            }
        }

        private static int CalculateWorthofList(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float missingValue)
        {
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
                    missingValue -= goodwill.amount * settings.goodwillWorth;
                }
                else if (reward is Reward_RoyalFavor favor)
                {

                    //// if it's Favour ignore goodwill.
                    if (settings.honourIgnoresGoodwill)
                    {
                        missingValue = 0;
                        break;
                    }
                    else
                    {
                        //// Decrease reward
                        missingValue -= favor.amount * settings.honourWorth;
                    }
                }
                else if (reward is Reward_Pawn pawn)
                {
                    //// Decrease reward by the value of the pawn - For Some Reason it's special
                    missingValue -= pawn.pawn.MarketValue;
                }
                else if (reward is Reward_CampLoot)
                {
                    if (settings.campLootIgnoresGoodwill)
                    {
                        missingValue = 0;
                        break;
                    }
                    else
                    {
                        missingValue -= (settings.campLootProcentValue / 100) * parms.rewardValue;
                    }
                }
                else
                {
                    //// Decrease reward by item value
                    missingValue -= reward.TotalMarketValue;
                }
                counter++;
            }

            return goodwillIndex;
        }

        public static float CalculateWorth(Reward reward, Goodwill_Settings settings, RewardsGeneratorParams parms)
        {
            float rewardAmount = 0;
            //// Find out if it's goodwill
            if (reward is Reward_Goodwill goodwill)
            {
                rewardAmount += goodwill.amount * settings.goodwillWorth;
            }
            else if (reward is Reward_RoyalFavor favor)
            {
                //// if it's Favour ignore goodwill.
                if (settings.honourIgnoresGoodwill)
                {
                    rewardAmount = 0;
                }
                else
                {
                    //// Decrease reward by 200 times the favor
                    rewardAmount += favor.amount * settings.honourWorth;
                }
            }
            else if (reward is Reward_Pawn pawn)
            {
                //// Decrease reward by the value of the pawn - For Some Reason it's special
                rewardAmount += pawn.pawn.MarketValue;
            }
            else if (reward is Reward_CampLoot)
            {
                if (settings.campLootIgnoresGoodwill)
                {
                    rewardAmount = 0;
                }
                else
                {
                    rewardAmount += (settings.campLootProcentValue / 100) * parms.rewardValue;
                }
            }
            else
            {
                //// Decrease reward by item value
                rewardAmount += reward.TotalMarketValue;
            }
            return rewardAmount;
        }

        private static void GenerateNewItems(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float missingValue)
        {
            Reward_Items items = new Reward_Items();
            float unaccountedAfterGoodwill = missingValue;
            float worthOfItemsGenerated;
            float originalRewardValue = parms.rewardValue;
            float originalMinGenRew = parms.minGeneratedRewardValue;
            parms.rewardValue = unaccountedAfterGoodwill;
            parms.minGeneratedRewardValue = unaccountedAfterGoodwill * 0.5f;
            items.InitFromValue(unaccountedAfterGoodwill, parms, out worthOfItemsGenerated); //// future me, yes the worth is accurate.
            parms.rewardValue = originalRewardValue;
            parms.minGeneratedRewardValue = originalMinGenRew;

            missingValue -= worthOfItemsGenerated;

            AddItems(__result, items);

            //// Debugging
            if (settings.debuggingOverflow)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Quests Give Goodwill Overflow:");
                foreach (Thing t in items.items)
                {
                    sb.AppendLine(t.Label + " worth: " + t.MarketValue * t.stackCount);
                }
                if (sb.Length > 0)
                    Log.Message(sb.ToString());
                else
                    Log.Message("Quests Give Goodwill Overflow: Tried to Generate items, but couldn't generate item worth at least:" + originalMinGenRew + " but looking for " + missingValue);
            }
        }
        private static void ReduceItems(List<Reward> items, ref float missingValue)
        {
            //// No Items? No need
            if(items.Count == 0) return;

            List<(float mValue, Thing thing)> sortingList = new List<(float, Thing)>();
            foreach (var item in items)
            {
                if (item is Reward_Items reward_item)
                {
                    foreach (var item1 in reward_item.items)
                    {
                        sortingList.Add((item1.MarketValue, item1));
                    }
                }
            }
            if (sortingList.Count == 0 || missingValue > 0)
            {
                return;
            }

            // Sort
            sortingList.SortByDescending(thing => thing.mValue);

            // The best fit discard helper
            DiscardHelper discardHelper = new DiscardHelper(-missingValue, settings.debuggingVerbose, settings.cleanupItemWeight);

            // for building up a strin to the debug.
            StringBuilder debugStrb = new StringBuilder();
            if (settings.debuggingVerbose)
            {
                debugStrb.AppendLine("SR_QGG: Value to trim: " + (-missingValue) + " - All items: ");
                try
                {
                    sortingList.OrderBy(x => x.mValue).Do((thing) => { debugStrb.AppendLine(thing.thing.stackCount + "x " + thing.thing.GetCustomLabelNoCount() + " (" + thing.mValue + "/u)"); });
                }
                catch { }
            }

            // Initiate the construction of the many examples.
            // The way this works is by recursively calling the recursiveAdding, which only adds to the list at the end of the sorting list OR if it has exceeded it's cost.
            for (int i = 0; i < sortingList.Count; i++)
            {
                RecursiveAdding(ref discardHelper, sortingList, new DiscardHelper(-missingValue, settings.debuggingVerbose, settings.cleanupItemWeight), i, debugStrb);
            }


            // Debugging looking at all the posibilities.
            if (settings.debuggingVerbose)
            {
                debugStrb.AppendLine();
                debugStrb.AppendLine("Planned to be removed:");
                debugStrb.AppendLine(discardHelper.ToString());
            }

            // Only executing the one found the most worth.
            discardHelper.Execute(items, ref missingValue);
            discardHelper = null;


            // Debugging looking at all the posibilities.
            if (settings.debuggingVerbose)
            {
                debugStrb.AppendLine("Final Reward: " + RewardsToString(items));
                Log.Message(debugStrb.ToString());
                debugStrb.Clear();
            }
        }

        private static string RewardsToString(List<Reward> items)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var reward in items)
            {
                if (reward is Reward_ArchonexusMap castRewardAM)
                {
                    sb.AppendLine($"{castRewardAM.currentPart.ToString()}th ArchonexusMap");
                }
                else if (reward is Reward_Goodwill castRewardGW)
                {
                    sb.AppendLine($"{castRewardGW.amount}x Goodwill with {castRewardGW.faction} worth {castRewardGW.amount * settings.goodwillWorth}");
                }
                else if (reward is Reward_Items castRewardItems)
                {
                    foreach (var item in castRewardItems.items)
                    {
                        sb.AppendLine($"{item.stackCount}x {item.GetCustomLabelNoCount()} worth {item.MarketValue * item.stackCount}");
                    }
                }
                else
                {
                    sb.AppendLine($"{reward.ToStringSafe()}");
                }
            }
            return sb.ToString();
        }

        private static void RecursiveAdding(ref DiscardHelper currentBestDiscard, List<(float mValue, Thing thing)> choiceItems, DiscardHelper currentChecker, int currentIndex, StringBuilder debugStrB)
        {
            // max of this items we could remove
            int maxItems = (int)Math.Ceiling((currentChecker.wantedValue - currentChecker.GetValue()) / choiceItems[currentIndex].mValue);

            // Limit it to the amount we have.
            maxItems = Math.Min(maxItems, choiceItems[currentIndex].thing.stackCount);

            // if no items can be added
            if (maxItems <= 0)
            {
                currentChecker.ReplaceIfBetter(ref currentBestDiscard, debugStrB);
                return;
            }
            // if at last item
            else if (currentIndex + 1 == choiceItems.Count)
            {
                // make a copy so we can have max items and 1 less.
                DiscardHelper otherChecker = currentChecker.getCopy();

                // add the ceiling
                currentChecker.AddToList(maxItems, choiceItems[currentIndex].thing);
                currentChecker.ReplaceIfBetter(ref currentBestDiscard, debugStrB);

                // add the floor
                if (maxItems > 1 && (currentChecker.GetValue() > currentChecker.wantedValue))
                {
                    otherChecker.AddToList(maxItems - 1, choiceItems[currentIndex].thing);
                    otherChecker.ReplaceIfBetter(ref currentBestDiscard, debugStrB);
                }

                return;
            }

            // Steps to reduce by, min 1, otherwise at least 100 silver a go.
            int step = (int)Math.Max(100 / Math.Floor(choiceItems[currentIndex].mValue), 1);

            // The Recursive part of this function, add j count of item at Index, then call it with the next index again.
            for (int j = maxItems; j >= 0; j -= step)
            {
                DiscardHelper helperCopy = currentChecker.getCopy();
                if (j > 0)
                    helperCopy.AddToList(j, choiceItems[currentIndex].thing);
                RecursiveAdding(ref currentBestDiscard, choiceItems, helperCopy, currentIndex + 1, debugStrB);
            }
        }

        private static void AddItems(List<Reward> __result, Reward_Items items)
        {
            //// Find out if there's already an item in the rewards list.
            Reward_Items resultItems = null;
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] is Reward_Items rItems)
                {
                    resultItems = rItems;
                }
            }

            //// it items are rewarded in the quest reward.
            if (resultItems != null)
            {
                //// for each new reward
                for (int i = 0; i < items.items.Count; i++)
                {
                    Thing thing = items.items[i];
                    //// check if item can be stacked with existing
                    int itemIndexInItems = resultItems.items.FindIndex((resultItem) => resultItem.CanStackWith(items.items[i]));
                    if (itemIndexInItems >= 0)
                    {
                        Thing resultThing = resultItems.items[itemIndexInItems];
                        resultThing.stackCount += thing.stackCount;
                    }
                    //// otherwise add it to the items in the rewards
                    else
                    {
                        resultItems.items.Add(thing);
                    }
                }
            }
            //// if no items are rewarded for the quest yet.
            else
            {
                __result.Add(items);
            }
        }

        private static void EnsureRewardWorth(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float missingValue)
        {
            if (missingValue > 0 && settings.boostRewards)
            {
                missingValue -= Grant_silver_reward(__result, settings, missingValue);
            }
        }

        private static float Grant_silver_reward(List<Reward> __result, Goodwill_Settings settings, float silverWanted)
        {
            if (silverWanted > 0 && settings.enableSilverRemainder)
            {
                Reward_Items items = new Reward_Items();
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = (int)silverWanted;
                items.items.Add(silver);
                AddItems(__result, items);

                //// Debugging
                if (settings.debuggingOverflow)
                {
                    Log.Message("Granted some Silver: " + silver.Label);
                }
                return silverWanted;
            }

            return 0;
        }

        // unused due to thinking initialising a params and copying gave error quests, confirmed false.
        private static void CopyParms(RewardsGeneratorParams parms, RewardsGeneratorParams newParm)
        {
            newParm.allowDevelopmentPoints = parms.allowDevelopmentPoints;
            newParm.allowGoodwill = parms.allowGoodwill;
            newParm.allowRoyalFavor = parms.allowRoyalFavor;
            newParm.allowXenogermReimplantation = parms.allowXenogermReimplantation;
            newParm.giverFaction = parms.giverFaction;
            newParm.giveToCaravan = parms.giveToCaravan;
            newParm.rewardValue = parms.rewardValue;
            newParm.allowRoyalFavor = parms.allowRoyalFavor;
            newParm.minGeneratedRewardValue = parms.minGeneratedRewardValue;
            newParm.thingRewardDisallowed = parms.thingRewardDisallowed;
            newParm.disallowedThingDefs = parms.disallowedThingDefs;
            newParm.chosenPawnSignal = parms.chosenPawnSignal;
        }

        private static int TypeIndexInList(List<Reward> __result, Type type)
        {
            int counter = 0;

            foreach (Reward reward in __result)
            {
                if(reward != null && type.IsAssignableFrom(reward.GetType()))
                {
                    return counter;
                }
                counter++;
            }

            return -1;
        }
    }
}
