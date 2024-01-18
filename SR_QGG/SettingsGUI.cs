using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

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
        /// Settings Strings
        /// </summary>
        private string extraFlatGoodwillText;
        private string extraproGoodwillText;

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
            //listingStandard.Indent();
            //listingStandard.ColumnWidth = listingStandard.ColumnWidth - 10;
            listingStandard.CheckboxLabeled("Can Goodwill be negative? (default Yes)", ref settings.canGoodwillBeNegative, "Sometimes quest rewards are worth more than the quest is worth, I take this as the quest givers are desperate, but unhappy about the price.");
            listingStandard.Label("What's the worth of goodwill in items? " + settings.goodwillWorth + " (Default 100)");
            settings.goodwillWorth = (int) listingStandard.Slider(settings.goodwillWorth, 50, 500);
            listingStandard.Label(settings.extraGoodwillPro + "% extra wealth for goodwill. (negative in case you want to roleplay hostile world) (Default 20%)");
            listingStandard.TextFieldNumeric(ref settings.extraGoodwillPro, ref extraproGoodwillText, -100f, 200f);
            //settings.extraGoodwillPro = listingStandard.Slider(settings.extraGoodwillPro, -100f, 200f); //// I can make it work but only be stealing code, which I don't wanna.
            listingStandard.Label(settings.extraGoodwillFlat + " flat extra goodwill on all quest rewards. (Default 2)");
            listingStandard.TextFieldNumeric(ref settings.extraGoodwillFlat, ref extraFlatGoodwillText, -10, 10);

            //// Honour
            //listingStandard.Outdent();
            //listingStandard.ColumnWidth = listingStandard.ColumnWidth + 10;
            listingStandard.Label(""); //// just for some space
            listingStandard.Label("Honour: ");
            //listingStandard.Indent();
            //listingStandard.ColumnWidth = listingStandard.ColumnWidth - 10;
            listingStandard.Label("When Honour is offered, should it Only be honour that's offered? (Default Yes)");
            listingStandard.CheckboxLabeled("Goodwill cannot be offered with Honour: ", ref settings.honourIgnoresGoodwill);
            listingStandard.Label("What's the worth of honour in items? (Default 100)" + settings.honourWorth);
            settings.honourWorth = (int) listingStandard.Slider(settings.honourWorth, 50, 500);
            //listingStandard.Outdent();
            //listingStandard.ColumnWidth = listingStandard.ColumnWidth + 10;

            //// Debugging
            listingStandard.CheckboxLabeled("Debugging Overflow", ref settings.debuggingOverflow, "Default No. Not needed by anyone but sir Rolin.");


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
            return "QuestsGiveGoodwill";
        }
    }
}
