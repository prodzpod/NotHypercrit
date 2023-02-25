using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API.Utils;
using RoR2;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace NotHypercrit
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod)]
    [BepInDependency("com.xoxfaby.BetterUI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.themysticsword.mysticsitems", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Hayaku.VanillaRebalance", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.TeamMoonstorm.Starstorm2-Nightly", BepInDependency.DependencyFlags.SoftDependency)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "Hypercrit2";
        public const string PluginVersion = "1.2.4";
        public static ManualLogSource Log;
        internal static PluginInfo pluginInfo;
        public static ConfigFile Config;

        public enum CritStackingMode { Linear, Exponential, Hyperbolic, Asymptotic };

        public static ConfigEntry<bool> CritEnable;
        public static ConfigEntry<float> CritCap;
        public static ConfigEntry<float> CritBase;
        public static ConfigEntry<float> CritMult;
        public static ConfigEntry<float> CritDecay;
        public static ConfigEntry<CritStackingMode> CritMode;
        public static ConfigEntry<bool> CritFraction;
        public static ConfigEntry<bool> CritDamageBonus;
        public static ConfigEntry<float> CritProcBase;
        public static ConfigEntry<float> CritProcMult;
        public static ConfigEntry<float> CritProcDecay;
        public static ConfigEntry<CritStackingMode> CritProcMode;
        public static ConfigEntry<int> CritColor;

        public static ConfigEntry<bool> Flurry;
        public static ConfigEntry<float> LaserScope;
        public static ConfigEntry<float> Moonglasses;

        public static ConfigEntry<bool> BleedEnable;
        public static ConfigEntry<float> BleedCap;
        public static ConfigEntry<float> BleedBase;
        public static ConfigEntry<float> BleedMult;
        public static ConfigEntry<float> BleedDecay;
        public static ConfigEntry<CritStackingMode> BleedMode;
        public static ConfigEntry<bool> BleedFraction;
        public static ConfigEntry<bool> BleedDamageBonus;
        public static ConfigEntry<float> BleedProcBase;
        public static ConfigEntry<float> BleedProcMult;
        public static ConfigEntry<float> BleedProcDecay;
        public static ConfigEntry<CritStackingMode> BleedProcMode;
        public static ConfigEntry<int> BleedColor;
        public static ConfigEntry<float> BleedStackBase;
        public static ConfigEntry<float> BleedStackMult;
        public static ConfigEntry<float> BleedStackDecay;
        public static ConfigEntry<CritStackingMode> BleedStackMode;

        public static ConfigEntry<bool> CollapseEnable;
        public static ConfigEntry<float> CollapseCap;
        public static ConfigEntry<float> CollapseBase;
        public static ConfigEntry<float> CollapseMult;
        public static ConfigEntry<float> CollapseDecay;
        public static ConfigEntry<CritStackingMode> CollapseMode;
        public static ConfigEntry<bool> CollapseFraction;
        public static ConfigEntry<bool> CollapseDamageBonus;
        public static ConfigEntry<float> CollapseProcBase;
        public static ConfigEntry<float> CollapseProcMult;
        public static ConfigEntry<float> CollapseProcDecay;
        public static ConfigEntry<CritStackingMode> CollapseProcMode;
        public static ConfigEntry<float> CollapseStackBase;
        public static ConfigEntry<float> CollapseStackMult;
        public static ConfigEntry<float> CollapseStackDecay;
        public static ConfigEntry<CritStackingMode> CollapseStackMode;

        public static ConfigEntry<bool> HyperbolicCrit;
        public static ConfigEntry<bool> HyperbolicBleed;
        public static ConfigEntry<bool> HyperbolicCollapse;
        public static ConfigEntry<bool> LamerShatterspleen;

        public class AdditionalProcInfo : R2API.Networking.Interfaces.ISerializableObject
        {
            public float totalChance = 0f;
            public int num = 0;
            public float damageMult = 1f;
            public int numProcs = 0;
            public void Deserialize(NetworkReader reader)
            {
                totalChance = reader.ReadSingle();
                num = reader.ReadInt32();
                numProcs = reader.ReadInt32();
                damageMult = reader.ReadSingle();
            }
            public void Serialize(NetworkWriter writer)
            {
                writer.Write(totalChance);
                writer.Write(num);
                writer.Write(numProcs);
                writer.Write(damageMult);
            }
        }

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
            CritEnable = Config.Bind("Hypercrit 2", "Enable", true, "Enables hypercrit.");
            CritCap = Config.Bind("Hypercrit 2", "Crit Cap", -1f, "Maximum number of crits. set to -1 to uncap.");
            CritBase = Config.Bind("Hypercrit 2", "Initial Multiplier", 2f, "yeah");
            CritMult = Config.Bind("Hypercrit 2", "Value", 1f, "refer to hypercrit mode");
            CritDecay = Config.Bind("Hypercrit 2", "Decay", 1f, "refer to hypercrit mode");
            CritMode = Config.Bind("Hypercrit 2", "Mode", CritStackingMode.Linear, "Linear: Base + (Mult*(Count - 1)), Exponential: Base * Pow(Mult, Count - 1), Hyperbolic: Base + (Mult - Mult / (1 + ((Decay / Mult) / (1 - (Decay / Mult))) * Count)) Asymtotic: Base + Mult * (1 - 2 ^ (-Count / Decay))");
            CritFraction = Config.Bind("Hypercrit 2", "Fraction Multiplier", false, "Whether fractional crit chance should contribute instead of in 100% increments.");
            CritDamageBonus = Config.Bind("Hypercrit 2", "Crit Damage Affects Hypercrit", true, "please turn off for asymptotic");
            CritProcBase = Config.Bind("Hypercrit 2", "Initial Proc", 1f, "value for proc");
            CritProcMult = Config.Bind("Hypercrit 2", "Proc Value", 1f, "value for proc");
            CritProcDecay = Config.Bind("Hypercrit 2", "Proc Decay", 1f, "refer to hypercrit mode");
            CritProcMode = Config.Bind("Hypercrit 2", "Proc Mode", CritStackingMode.Linear, "mode for proc, HIGHLY do not recommend exponential PLEASE");
            CritColor = Config.Bind("Hypercrit 2", "Color Cycle", 12, "Set to 1 to disable color change");

            Flurry = Config.Bind("Hypercrit 2", "Procs Affects Flurry", true, "yeah!!");
            LaserScope = Config.Bind("Hypercrit 2", "Laser Scope Crit on First Stack", 25f, "Gives crit chance on first stack, like other crit synergy items.");
            Moonglasses = Config.Bind("Hypercrit 2", "Moonglasses Rework", 50f, "makes it so moonglasses reduces crit chance. actual downside?? set to 0 to disable.");
            HyperbolicCrit = Config.Bind("Hypercrit 2", "Hyperbolic Crit", false, "makes crit hyperbolic (nerf). DISABLES CRIT SETTING");

            BleedEnable = Config.Bind("Hyperbleed 2", "Enable", true, "Enables hyperbleed.");
            BleedCap = Config.Bind("Hyperbleed 2", "Bleed Cap", -1f, "Maximum number of bleed chance. set to -1 to uncap.");
            BleedBase = Config.Bind("Hyperbleed 2", "Initial Multiplier", 1f, "yeah");
            BleedMult = Config.Bind("Hyperbleed 2", "Value", 1f, "refer to hyperbleed mode");
            BleedDecay = Config.Bind("Hyperbleed 2", "Decay", 1f, "refer to hyperbleed mode");
            BleedMode = Config.Bind("Hyperbleed 2", "Mode", CritStackingMode.Linear, "Linear: Base + (Mult*(Count - 1)), Exponential: Base * Pow(Mult, Count - 1), Hyperbolic: Base + (Mult - Mult / (1 + ((Decay / Mult) / (1 - (Decay / Mult))) * Count)) Asymtotic: Base + Mult * (1 - 2 ^ (-Count / Decay))");
            BleedFraction = Config.Bind("Hyperbleed 2", "Fraction Multiplier", true, "Whether fractional bleed chance should contribute to damage instead of in 100% increments.");
            BleedDamageBonus = Config.Bind("Hyperbleed 2", "Bleed Damage Affects Hyperbleed", true, "please turn off for asymptotic");
            BleedProcBase = Config.Bind("Hyperbleed 2", "Initial Proc", 1f, "value for proc");
            BleedProcMult = Config.Bind("Hyperbleed 2", "Proc Value", 1f, "value for proc");
            BleedProcDecay = Config.Bind("Hyperbleed 2", "Proc Decay", 1f, "refer to hyperbleed mode");
            BleedProcMode = Config.Bind("Hyperbleed 2", "Proc Mode", CritStackingMode.Linear, "mode for proc, HIGHLY do not recommend exponential PLEASE");
            BleedColor = Config.Bind("Hyperbleed 2", "Color Cycle", 6, "Set to 1 to disable color change");
            BleedStackBase = Config.Bind("Hyperbleed 2", "Stack Initial Multiplier", 1f, "Multiple stacks of bleed stacks differently?!!");
            BleedStackMult = Config.Bind("Hyperbleed 2", "Stack Value", 1f, "refer to hyperbleed stack mode");
            BleedStackDecay = Config.Bind("Hyperbleed 2", "Stack Decay", 1f, "refer to hyperbleed stack mode");
            BleedStackMode = Config.Bind("Hyperbleed 2", "Stack Mode", CritStackingMode.Linear, "Linear: Base + (Mult*(Count - 1)), Exponential: Base * Pow(Mult, Count - 1), Hyperbolic: Base + (Mult - Mult / (1 + ((Decay / Mult) / (1 - (Decay / Mult))) * Count)) Asymtotic: Base + Mult * (1 - 2 ^ (-Count / Decay))");

            CollapseEnable = Config.Bind("Hypercollapse 2", "Enable Collapse", true, "Enables hypercollapse.");
            CollapseCap = Config.Bind("Hypercollapse 2", "Collapse Cap", -1f, "Maximum number of collapse chance. set to -1 to uncap.");
            CollapseBase = Config.Bind("Hypercollapse 2", "Initial Multiplier", 1f, "yeah");
            CollapseMult = Config.Bind("Hypercollapse 2", "Value", 1f, "refer to hypercollapse mode");
            CollapseDecay = Config.Bind("Hypercollapse 2", "Decay", 1f, "refer to hypercollapse mode");
            CollapseMode = Config.Bind("Hypercollapse 2", "Mode", CritStackingMode.Linear, "Linear: Base + (Mult*(Count - 1)), Exponential: Base * Pow(Mult, Count - 1), Hyperbolic: Base + (Mult - Mult / (1 + ((Decay / Mult) / (1 - (Decay / Mult))) * Count)) Asymtotic: Base + Mult * (1 - 2 ^ (-Count / Decay))");
            CollapseFraction = Config.Bind("Hypercollapse 2", "Fraction Multiplier", true, "Whether fractional collapse chance should contribute to damage instead of in 100% increments.");
            CollapseDamageBonus = Config.Bind("Hypercollapse 2", "Collapse Damage Affects Hypercollapse", true, "please turn off for asymptotic");
            CollapseProcBase = Config.Bind("Hypercollapse 2", "Initial Proc", 1f, "value for proc");
            CollapseProcMult = Config.Bind("Hypercollapse 2", "Proc Value", 1f, "value for proc");
            CollapseProcDecay = Config.Bind("Hypercollapse 2", "Proc Decay", 1f, "refer to hypercollapse mode");
            CollapseProcMode = Config.Bind("Hypercollapse 2", "Proc Mode", CritStackingMode.Linear, "mode for proc, HIGHLY do not recommend exponential PLEASE");
            CollapseStackBase = Config.Bind("Hypercollapse 2", "Stack Initial Multiplier", 1f, "Multiple stacks of collapse stacks differently?!!");
            CollapseStackMult = Config.Bind("Hypercollapse 2", "Stack Value", 1f, "refer to hypercollapse stack mode");
            CollapseStackDecay = Config.Bind("Hypercollapse 2", "Stack Decay", 1f, "refer to hypercollapse stack mode");
            CollapseStackMode = Config.Bind("Hypercollapse 2", "Stack Mode", CritStackingMode.Linear, "Linear: Base + (Mult*(Count - 1)), Exponential: Base * Pow(Mult, Count - 1), Hyperbolic: Base + (Mult - Mult / (1 + ((Decay / Mult) / (1 - (Decay / Mult))) * Count)) Asymtotic: Base + Mult * (1 - 2 ^ (-Count / Decay))");

            HyperbolicBleed = Config.Bind("Hyperbleed 2", "Hyperbolic Bleed", false, "makes bleed hyperbolic (nerf). DISABLES BLEED SETTING");
            HyperbolicCollapse = Config.Bind("Hyperbleed 2", "Hyperbolic Collapse", false, "makes collapse hyperbolic (nerf). DISABLES COLLAPSE SETTING");
            LamerShatterspleen = Config.Bind("Hyperbleed 2", "Lamer Shatterspleen", true, "Shatterspleen adds crit chance to bleed chance instead of bleed doubleproccing.");

            if (Mods("com.xoxfaby.BetterUI")) BetterUICompat();
            if (LaserScope.Value != 0) Crit.LaserScopeRework();
            if (Mods("com.themysticsword.mysticsitems") && Moonglasses.Value != 0) Crit.MoonglassesRework();

            if (CritEnable.Value) Crit.Patch();
            if (BleedEnable.Value || CollapseEnable.Value) Bleed.Patch();
            if (HyperbolicCrit.Value) Crit.PatchHyperbolic();
            Bleed.PatchStack();
        }

        public static bool Mods(params string[] arr)
        {
            for (int i = 0; i < arr.Length; i++) if (!Chainloader.PluginInfos.ContainsKey(arr[i])) return false;
            return true;
        }

        public static float GetCollapse(CharacterBody body)
        {
            return HyperbolicCollapse.Value ?
                (10f - (10f / (1f + (0.011111f * (body.inventory.GetItemCount(DLC1Content.Items.BleedOnHitVoid) + (body.HasBuff(DLC1Content.Buffs.EliteVoid) ? 10 : 0))))))
                : ((body.inventory.GetItemCount(DLC1Content.Items.BleedOnHitVoid) + (body.HasBuff(DLC1Content.Buffs.EliteVoid) ? 10 : 0)) * 10);
        }

        public static float GetWLuck(float orig, CharacterBody body)
        {
            float chance = 100 * (int)orig + 100 * BetterUI.Utils.LuckCalc(orig % 1, body.master.luck);
            if (chance > 0) chance += (body?.inventory?.GetItemCount(ItemCatalog.FindItemIndex("MysticsItems_ScratchTicket")) ?? 0);
            return chance;
        }

        public static void BetterUICompat()
        {
            BetterUI.StatsDisplay.regexmap["$luckcrit"] = statBody => GetWLuck(statBody.crit / 100f, statBody).ToString("0.##");
            BetterUI.StatsDisplay.regexmap.Add("$hypercrit", statBody => Crit.GetDamage(statBody.crit, statBody.critMultiplier - 2, statBody).ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$bleed", statBody => statBody.bleedChance.ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$collapse", statBody => (GetCollapse(statBody) * 100).ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$luckbleed", statBody => GetWLuck(statBody.bleedChance / 100f, statBody).ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$luckcollapse", statBody => GetWLuck(GetCollapse(statBody), statBody).ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$bleeddamage", statBody => statBody.GetBleedDamage().ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$collapsedamage", statBody => statBody.GetCollapseDamage().ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$hyperbleed", statBody => Bleed.GetDamage(statBody.bleedChance, statBody.GetBleedDamage() - 1, statBody).ToString("0.##"));
            BetterUI.StatsDisplay.regexmap.Add("$hypercollapse", statBody => Bleed.GetDamage(GetCollapse(statBody) * 100, statBody.GetCollapseDamage() - 1, statBody).ToString("0.##"));
            var sortedKeys = BetterUI.StatsDisplay.regexmap.Keys.ToList();
            sortedKeys.Sort((s1, s2) => s2.Length - s1.Length);
            BetterUI.StatsDisplay.regexpattern = new Regex(@"(\" + string.Join(@"|\", sortedKeys) + ")");
        }

        public static float Calc(CritStackingMode mode, float init, float mult, float decay, float count)
        {
            switch (mode)
            {
                case CritStackingMode.Linear:
                    return init + mult * (count - 1);
                case CritStackingMode.Exponential:
                    return init * Mathf.Pow(mult, count - 1);
                case CritStackingMode.Hyperbolic:
                    return init + (mult * decay * count / (decay * count + mult - decay));
                case CritStackingMode.Asymptotic:
                    return init + mult * (1f - Mathf.Pow(2, -(count - 1) / decay));
            }
            Log.LogError("Invalid Mode??");
            return 0;
        }
    }
}
