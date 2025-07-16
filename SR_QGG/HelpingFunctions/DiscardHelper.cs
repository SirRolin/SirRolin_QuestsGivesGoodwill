using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SirRolin.QuestsGiveGoodwill.HelpingFunctions
{
    public class DiscardHelper
    {
        public List<(int count, float unitValue, Thing thing)> Things = new List<(int count, float unitValue, Thing thing)>();
        public float wantedValue;
        private static bool _debug;
        private static int _itemWeight;
        public DiscardHelper(float wantedDiscard, bool debug, int itemWeight)
        {
            this.wantedValue = wantedDiscard;
            _debug = debug;
            _itemWeight = itemWeight;
        }
        private DiscardHelper(float wantedDiscard)
        {
            this.wantedValue = wantedDiscard;
        }
        public void AddToList(int amount, Thing thing)
        {
            Things.Add((amount, thing.MarketValue, thing));
        }
        public DiscardHelper getCopy()
        {
            DiscardHelper output = new DiscardHelper(wantedValue);
            Things.ForEach((each) =>
            {
                output.AddToList(each.count, each.thing);
            });
            return output;
        }
        public float GetValue()
        {
            return Things.Sum(x => x.count * x.unitValue);
        }
        public float getWeightedValue()
        {
            return Math.Abs(Things.Sum(x => x.count * x.unitValue) - wantedValue) + _itemWeight * Things.Sum(x => x.count);
        }
        public void ReplaceIfBetter(ref DiscardHelper dh, StringBuilder debugStrB)
        {
            if (dh.getWeightedValue() > getWeightedValue())
            {
                if (_debug) debugStrB.Append("Discarded for new:\n" + dh.ToString());
                dh = this;
            }
            else if (_debug && (dh.Things.Count != Things.Count || dh.GetValue() != GetValue()))
            {
                debugStrB.Append("Doesn't make it:\n" + ToString());
            }
        }
        public void Execute(List<Reward> ri, ref float missingValue)
        {
            missingValue += GetValue();

            Things.ForEach(thing =>
            {
                thing.thing.stackCount -= thing.Item1;
            });

            for (int j = ri.Count - 1; j >= 0; j--)
            {
                if (ri[j] is Reward_Items list)
                {
                    for (int i = list.items.Count - 1; i >= 0; i--)
                    {
                        if (list.items[i].stackCount == 0)
                        {
                            list.items.RemoveAt(i);
                        }
                    }
                }
            }
        }
        override public String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Weight: " + getWeightedValue() + " - worth: " + GetValue());
            foreach (var (count, unitValue, thing) in Things)
            {
                sb.AppendLine(count.ToString() + "x " + thing.GetCustomLabelNoCount() + " (" + Math.Round(count * unitValue, 2) + ", " + Math.Round(unitValue, 2) + "/u)");
            }

            return sb.ToString();
        }
    }
}
