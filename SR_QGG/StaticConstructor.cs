using HarmonyLib;
using Multiplayer.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SirRolin.QuestsGiveGoodwill
{

    [StaticConstructorOnStartup]
    public class StaticConstructor
    {
        static StaticConstructor()
        {
            Harmony val = new Harmony("SirRolin.QuestGiveGoodwill");
            val.PatchAll();
            if (MP.enabled)
            {
                MP.RegisterAll();
            }
        }
    }
}
