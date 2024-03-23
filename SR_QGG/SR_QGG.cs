using System.Collections.Generic;

using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;

[StaticConstructorOnStartup]
public class PatchMain
{
    static PatchMain()
    {
        Harmony val = new Harmony("SR.QGG");
        val.PatchAll();
    }
}