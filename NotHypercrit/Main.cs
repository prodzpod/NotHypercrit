using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API;
using R2API.Utils;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace NotHypercrit
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod)]
    [BepInDependency("Hayaku.VanillaRebalance", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("droppod.lookingglass", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.themysticsword.mysticsitems", BepInDependency.DependencyFlags.SoftDependency)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "Hypercrit2";
        public const string PluginVersion = "2.0.0";
        public static ManualLogSource Log;
        internal static PluginInfo pluginInfo;
        public static ConfigFile Config;

        public enum CritStackingMode { Basic, Exponential, Cumulative, Asymptotic, Logarithmic };
        public enum CritChanceMode { Rollover, Uniform, Binomial, Halves, ReverseHalves };

        public static ConfigEntry<float> BaseCritChance;
        public static ConfigEntry<float> CritCap;
        public static ConfigEntry<CritStackingMode> CritMode;
        public static ConfigEntry<float> CritA;
        public static ConfigEntry<float> CritB;
        public static ConfigEntry<CritStackingMode> CritProcMode;
        public static ConfigEntry<float> CritProcA;
        public static ConfigEntry<float> CritProcB;
        public static ConfigEntry<CritChanceMode> CritChance;
        public static ConfigEntry<int> CritColor;

        public static ConfigEntry<bool> Flurry;
        public static ConfigEntry<float> LaserScope;
        public static ConfigEntry<float> LaserScopeStack;
        public static ConfigEntry<float> LaserScopeDamage;
        public static ConfigEntry<float> LaserScopeDamageStack;
        public static ConfigEntry<float> Moonglasses;

        public static ConfigEntry<float> BaseBleedChance;
        public static ConfigEntry<float> BleedCap;
        public static ConfigEntry<CritStackingMode> BleedMode;
        public static ConfigEntry<float> BleedA;
        public static ConfigEntry<float> BleedB;
        public static ConfigEntry<CritStackingMode> BleedProcMode;
        public static ConfigEntry<float> BleedProcA;
        public static ConfigEntry<float> BleedProcB;
        public static ConfigEntry<CritChanceMode> BleedChance;
        public static ConfigEntry<int> BleedColor;

        public static ConfigEntry<float> CollapseCap;
        public static ConfigEntry<CritStackingMode> CollapseMode;
        public static ConfigEntry<float> CollapseA;
        public static ConfigEntry<float> CollapseB;
        public static ConfigEntry<CritStackingMode> CollapseProcMode;
        public static ConfigEntry<float> CollapseProcA;
        public static ConfigEntry<float> CollapseProcB;
        public static ConfigEntry<CritChanceMode> CollapseChance;

        //public static ConfigEntry<bool> HyperbolicCrit;
        //public static ConfigEntry<bool> HyperbolicBleed;
        //public static ConfigEntry<bool> HyperbolicCollapse;
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
                /* leaving this for historical reasons
                foreach (var def in ItemCatalog.allItemDefs)
                    Debug.Log($"{Language.GetString(def.nameToken)} ({def.tier}): {Language.GetString(def.descriptionToken)}");
                foreach (var def in EquipmentCatalog.equipmentDefs)
                    Debug.Log($"{Language.GetString(def.nameToken)} ({(def.isLunar ? "LunarEquipment" : "Equipment")}): {Language.GetString(def.descriptionToken)}");
                */
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
            BaseCritChance = Config.Bind("Hypercrit 2", "Base Crit Chance", 0.01f, "Crit chance every survivors start with.");
            CritCap = Config.Bind("Hypercrit 2", "Crit Cap", -1f, "Maximum number of crits. set to -1 to uncap, set to 1 to disable.");
            CritMode = Config.Bind("Hypercrit 2", "Mode", CritStackingMode.Basic, "Crit damage calculation, See README for details");
            CritA = Config.Bind("Hypercrit 2", "A", 1f, "See README for details");
            CritB = Config.Bind("Hypercrit 2", "B", 1f, "See README for details");
            CritProcMode = Config.Bind("Hypercrit 2", "Proc Mode", CritStackingMode.Basic, "Crit proc amount calculation, See README for details");
            CritProcA = Config.Bind("Hypercrit 2", "Proc A", 1f, "See README for details");
            CritProcB = Config.Bind("Hypercrit 2", "Proc B", 1f, "See README for details");
            CritChance = Config.Bind("Hypercrit 2", "Crit Chance Distribution", CritChanceMode.Rollover, "See README for details");
            CritColor = Config.Bind("Hypercrit 2", "Color Cycle", 12, "Set to 1 to disable color change");

            Flurry = Config.Bind("Hypercrit 2", "Procs Affects Flurry", true, "yeah!!");
            LaserScope = Config.Bind("Hypercrit 2", "Laser Scope Crit Chance on First Stack", 25f, "Gives crit chance on first stack, like other crit synergy items.");
            LaserScopeStack = Config.Bind("Hypercrit 2", "Laser Scope Crit Chance on Subsequent Stack", 0f, "Gives crit chance on first stack, like other crit synergy items.");
            LaserScopeDamage = Config.Bind("Hypercrit 2", "Laser Scope Crit Damage on First Stack", 100f, "Gives crit chance on first stack, like other crit synergy items.");
            LaserScopeDamageStack = Config.Bind("Hypercrit 2", "Laser Scope Crit Damage on Subsequent Stack", 100f, "Gives crit chance on first stack, like other crit synergy items.");
            Moonglasses = Config.Bind("Hypercrit 2", "Moonglasses Rework", 50f, "makes it so moonglasses reduces crit chance. actual downside?? set to 0 to disable.");

            BaseBleedChance = Config.Bind("Hyperbleed 2", "Base Bleed Chance", 0f, "Bleed chance every survivors start with.");

            BleedCap = Config.Bind("Hyperbleed 2", "Bleed Cap", -1f, "Maximum number of bleeds. set to -1 to uncap, set to 1 to disable.");
            BleedMode = Config.Bind("Hyperbleed 2", "Mode", CritStackingMode.Basic, "Bleed damage calculation, See README for details");
            BleedA = Config.Bind("Hyperbleed 2", "A", 1f, "See README for details");
            BleedB = Config.Bind("Hyperbleed 2", "B", 1f, "See README for details");
            BleedProcMode = Config.Bind("Hyperbleed 2", "Proc Mode", CritStackingMode.Basic, "Bleed proc amount calculation, See README for details");
            BleedProcA = Config.Bind("Hyperbleed 2", "Proc A", 1f, "See README for details");
            BleedProcB = Config.Bind("Hyperbleed 2", "Proc B", 1f, "See README for details");
            BleedChance = Config.Bind("Hyperbleed 2", "Bleed Chance Distribution", CritChanceMode.Rollover, "See README for details");
            BleedColor = Config.Bind("Hyperbleed 2", "Color Cycle", 12, "Set to 1 to disable color change");


            CollapseCap = Config.Bind("Hypercollapse 2", "Collapse Cap", -1f, "Maximum number of collapses. set to -1 to uncap, set to 1 to disable.");
            CollapseMode = Config.Bind("Hypercollapse 2", "Mode", CritStackingMode.Basic, "Collapse damage calculation, See README for details");
            CollapseA = Config.Bind("Hypercollapse 2", "A", 1f, "See README for details");
            CollapseB = Config.Bind("Hypercollapse 2", "B", 1f, "See README for details");
            CollapseProcMode = Config.Bind("Hypercollapse 2", "Proc Mode", CritStackingMode.Basic, "Collapse proc amount calculation, See README for details");
            CollapseProcA = Config.Bind("Hypercollapse 2", "Proc A", 1f, "See README for details");
            CollapseProcB = Config.Bind("Hypercollapse 2", "Proc B", 1f, "See README for details");
            CollapseChance = Config.Bind("Hypercollapse 2", "Collapse Chance Distribution", CritChanceMode.Rollover, "See README for details");

            LamerShatterspleen = Config.Bind("Hyperbleed 2", "Lamer Shatterspleen", true, "Shatterspleen adds crit chance to bleed chance instead of bleed doubleproccing.");

            if (Mods("droppod.lookingglass")) LookingGlassCompat();
            if (LaserScope.Value != 0) Crit.LaserScopeRework();
            if (Mods("com.themysticsword.mysticsitems") && Moonglasses.Value != 0) Crit.MoonglassesRework();

            RecalculateStatsAPI.GetStatCoefficients += (self, args) =>
            {
                self.baseCrit = BaseCritChance.Value * 100;
                self.bleedChance += BaseBleedChance.Value * 100;
            };

            Crit.Patch();
            Bleed.Patch();
            Bleed.PatchStack();
        }

        public static bool Mods(params string[] arr)
        {
            for (int i = 0; i < arr.Length; i++) if (!Chainloader.PluginInfos.ContainsKey(arr[i])) return false;
            return true;
        }

        public static float GetCollapse(CharacterBody body)
        {
            return (body.inventory.GetItemCountEffective(DLC1Content.Items.BleedOnHitVoid) + (body.HasBuff(DLC1Content.Buffs.EliteVoid) ? 10 : 0)) * 10;
        }

        public static float GetWLuck(float orig, CharacterBody body)
        {
            float chance = 100 * (int)orig + 100 * LuckCalc(orig % 1, body.master.luck);
            if (chance > 0) chance += (body?.inventory?.GetItemCountEffective(ItemCatalog.FindItemIndex("MysticsItems_ScratchTicket")) ?? 0);
            return chance;
        }

        public static float LuckCalc(float chance, float luck)
        {
            if (luck == 0f) return chance;
            if (luck < 0f) return (int)chance + Mathf.Pow(chance % 1f, Math.Abs(luck) + 1f);
            return (int)chance + (1f - Mathf.Pow(1f - chance % 1f, Math.Abs(luck) + 1f));
        }

        public static void LookingGlassCompat()
        {
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary["critWithLuck"] = statBody => GetWLuck(statBody.crit / 100f, statBody).ToString("0.##");
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary["bleedChanceWithLuck"] = statBody => GetWLuck(statBody.bleedChance / 100f, statBody).ToString("0.##");
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("collapseChance", statBody => _d((GetCollapse(statBody) * 100).ToString("0.##")));
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("collapseChanceWithLuck", statBody => _d(GetWLuck(GetCollapse(statBody), statBody).ToString("0.##")));
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("bleedMultiplier", statBody => _d(statBody.GetBleedDamage().ToString("0.##")));
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("collapseMultiplier", statBody => _d(statBody.GetCollapseDamage().ToString("0.##")));
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("hypercrit", statBody => _d(Crit.GetDamage(statBody.crit, statBody.critMultiplier - 2, statBody).ToString("0.##")));
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("hyperbleed", statBody => _d(Bleed.GetDamage(statBody.bleedChance, statBody.GetBleedDamage() - 1, statBody).ToString("0.##")));
            LookingGlass.StatsDisplay.StatsDisplayClass.statDictionary.Add("hypercollapse", statBody => _d(Bleed.GetDamage(GetCollapse(statBody) * 100, statBody.GetCollapseDamage() - 1, statBody).ToString("0.##")));
            static string _d(string input) { return $"<style=\"cIsDamage>{input}</style>"; }
        }

        public static float Calc(CritStackingMode mode, float a, float b, float x)
        {
            if (x <= 0) return 1;
            switch (mode)
            {
                case CritStackingMode.Basic:
                    return 1 + (a * MathF.Pow(x, b));
                case CritStackingMode.Exponential:
                    return 1 + (a * MathF.Pow(b, x));
                case CritStackingMode.Cumulative:
                    float ret = 0;
                    for (int i = 1; i <= x; i++) ret += MathF.Pow(i, b);
                    return 1 + (a * ret);
                case CritStackingMode.Asymptotic:
                    return 1 + ((a - 1) * (1 - MathF.Pow(1 - b, x)));
                case CritStackingMode.Logarithmic:
                    return 1 + (a * MathF.Log(x + 1) / MathF.Log(b));
            }
            Log.LogError("Invalid Mode??");
            return 0;
        }

        public static int CalcChance(CritChanceMode mode, float count, float luck = 0)
        {
            var ci = Mathf.FloorToInt(count);
            var cf = count % 1f;
            var r = RollRaw(luck);
            if (ci == 0) return r < cf ? 1 : 0;
            switch (mode)
            {
                case CritChanceMode.Rollover:
                    return ci + (r < cf ? 1 : 0);
                case CritChanceMode.Uniform:
                    return 1 + Mathf.FloorToInt(r * (ci * (ci + 1)) / (ci + 1 - cf));
                case CritChanceMode.Binomial:
                {
                    var ret = 1;
                    for (int i = 0; i < ci; i++) ret += Util.CheckRoll(.5f, luck) ? 1 : 0;
                    ret += Util.CheckRoll(cf / 2, luck) ? 1 : 0;
                    return ret;
                }
                case CritChanceMode.Halves:
                { 
                    var m = r * (Mathf.Pow(2, ci + 1) - 2 + cf);
                    var ret = ci;
                    for (int i = 0; i < ci; i++) if (m > Mathf.Pow(2, i + 1)) ret -= 1;
                    return 1 + ret;
                }
                case CritChanceMode.ReverseHalves:
                {
                    var m = r * (Mathf.Pow(2, ci + 1) - 2 + cf);
                    var ret = 0;
                    for (int i = 0; i < ci; i++) if (m < Mathf.Pow(2, i + 1)) ret += 1;
                    return 1 + ret;
                }
            }
            Log.LogError("Invalid Mode??");
            return 0;
        }

        public static float RollRaw(float luck = 0)
        {
            var ret = UnityEngine.Random.Range(0f, 1f);
            var lc = Mathf.CeilToInt(Mathf.Abs(luck));
            for (var i = 0; i < lc; i++)
            {
                float b = UnityEngine.Random.Range(0f, 1f);
                ret = (luck > 0f) ? Mathf.Min(ret, b) : Mathf.Max(ret, b);
            }
            return ret;
        }
    }
}
