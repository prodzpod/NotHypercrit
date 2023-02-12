using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Orbs;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public class NotHypercritPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "Hypercrit2";
        public const string PluginVersion = "1.0.0";
        public static ManualLogSource Log;
        internal static PluginInfo pluginInfo;
        public static ConfigFile Config;
        public enum CritStackingMode { Linear, Exponential, Asymptotic };

        public static ConfigEntry<int> Cap;
        public static ConfigEntry<float> Base;
        public static ConfigEntry<float> Mult;
        public static ConfigEntry<CritStackingMode> Mode;
        public static ConfigEntry<bool> DamageBonus;
        public static ConfigEntry<int> Moonglasses;
        public static ConfigEntry<float> ProcBase;
        public static ConfigEntry<float> ProcMult;
        public static ConfigEntry<CritStackingMode> ProcMode;
        public static ConfigEntry<bool> Flurry;
        public static ConfigEntry<int> Color;

        public readonly ConditionalWeakTable<object, AdditionalCritInfo> critInfoAttachments = new ConditionalWeakTable<object, AdditionalCritInfo>();
        public static AdditionalCritInfo lastNetworkedCritInfo = null;
        public class AdditionalCritInfo : R2API.Networking.Interfaces.ISerializableObject
        {
            public float totalCritChance = 0f;
            public int numCrits = 0;
            public float damageMult = 1f;
            public int numProcs = 0;
            public void Deserialize(NetworkReader reader)
            {
                totalCritChance = reader.ReadSingle();
                numCrits = reader.ReadInt32();
                numProcs = reader.ReadInt32();
                damageMult = reader.ReadSingle();
            }
            public void Serialize(NetworkWriter writer)
            {
                writer.Write(totalCritChance);
                writer.Write(numCrits);
                writer.Write(numProcs);
                writer.Write(damageMult);
            }
        }

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
            Cap = Config.Bind("Hypercrit 2", "Crit Cap", -1, "Maximum number of crits. set to -1 to uncap.");
            Base = Config.Bind("Hypercrit 2", "Initial Multiplier", 2f, "yeah");
            Mult = Config.Bind("Hypercrit 2", "Value", 1f, "refer to hypercrit mode");
            Mode = Config.Bind("Hypercrit 2", "Mode", CritStackingMode.Linear, "Linear: Base + (Mult*(Count - 1)), Exponential: Base * Pow(Mult, Count - 1), Asymtotic: Base + Mult * (1 - 2 ^ (-Count))");
            DamageBonus = Config.Bind("Hypercrit 2", "Crit Damage Affects Hypercrit", true, "please turn off for asymptotic");
            Moonglasses = Config.Bind("Hypercrit 2", "Moonglasses Rework", 100, "makes it so moonglasses reduces crit chance. actual downside?? set to 0 to disable.");
            ProcBase = Config.Bind("Hypercrit 2", "Initial Proc", 1f, "value for proc");
            ProcMult = Config.Bind("Hypercrit 2", "Proc Value", 1f, "value for proc");
            ProcMode = Config.Bind("Hypercrit 2", "Proc Mode", CritStackingMode.Linear, "mode for proc, HIGHLY do not recommend exponential PLEASE");
            Flurry = Config.Bind("Hypercrit 2", "Procs Affects Flurry", true, "yeah!!");
            Color = Config.Bind("Hypercrit 2", "Color", 12, "Set to 1 to disable color change");

            Log.LogDebug("The Spirit of ThinkInvis Embraces You...");

            if (Mods("com.xoxfaby.BetterUI")) BetterUICompat();
            if (Mods("com.themysticsword.mysticsitems") && Moonglasses.Value != 0) MoonglassesRework();

            IL.RoR2.HealthComponent.TakeDamage += (il) =>
            {
                ILCursor c = new(il);
                int damageInfoIndex = -1;
                if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(out damageInfoIndex),
                    x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.crit)),
                    x => x.MatchBrfalse(out _),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdloc(1),
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_critMultiplier")) && damageInfoIndex != -1)
                {
                    c.Emit(OpCodes.Ldloc_1);
                    c.Emit(OpCodes.Ldarg, damageInfoIndex);
                    c.EmitDelegate<Func<float, CharacterBody, DamageInfo, float>>((orig, self, info) =>
                    {
                        if (!self) return orig;
                        AdditionalCritInfo aci = null;
                        if (!critInfoAttachments.TryGetValue(info, out aci))
                        {
                            aci = RollHypercrit(orig - 2f, self, true);
                            critInfoAttachments.Add(info, aci);
                        }
                        info.crit = aci.numCrits > 0;
                        return aci.damageMult;
                    });
                }
            };

            IL.RoR2.HealthComponent.SendDamageDealt += (il) => {
                var c = new ILCursor(il);
                c.GotoNext(MoveType.After,
                    x => x.MatchNewobj<DamageDealtMessage>());
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<DamageDealtMessage, DamageReport>>((msg, report) => {
                    TryPassHypercrit(report.damageInfo, msg);
                });
            };
            On.RoR2.DamageDealtMessage.Serialize += (orig, self, writer) => {
                orig(self, writer);
                AdditionalCritInfo aci;
                if (!critInfoAttachments.TryGetValue(self, out aci)) aci = new AdditionalCritInfo();
                aci.Serialize(writer);
            };
            On.RoR2.DamageDealtMessage.Deserialize += (orig, self, reader) => {
                orig(self, reader);
                AdditionalCritInfo aci = new AdditionalCritInfo();
                aci.Deserialize(reader);
                critInfoAttachments.Add(self, aci);
                lastNetworkedCritInfo = aci;
            };
            IL.RoR2.DamageNumberManager.SpawnDamageNumber += (il) => {
                var c = new ILCursor(il);
                c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(DamageColor), nameof(DamageColor.FindColor)));
                c.EmitDelegate<Func<Color, Color>>((origColor) => {
                    if (lastNetworkedCritInfo == null) return origColor;
                    var aci = lastNetworkedCritInfo;
                    lastNetworkedCritInfo = null;
                    if (aci.numCrits == 0) return origColor;
                    float h = 1f / 6f - (aci.numCrits - 1f) / Color.Value;
                    return UnityEngine.Color.HSVToRGB(((h % 1f) + 1f) % 1f, 1f, 1f);
                });
            };
            On.RoR2.GlobalEventManager.OnCrit += (orig, self, body, damageInfo, master, procCoefficient, procChainMask) =>
            {
                critInfoAttachments.TryGetValue(damageInfo, out var aci);
                if (aci != null && aci.numProcs > 1)
                {
                    int procs = aci.numProcs;
                    aci.numProcs = 0;
                    for (var i = 0; i < procs; i++) orig(self, body, damageInfo, master, procCoefficient, procChainMask);
                }
                else orig(self, body, damageInfo, master, procCoefficient, procChainMask);
            };
            if (Flurry.Value)
            {
                On.EntityStates.Huntress.HuntressWeapon.FireFlurrySeekingArrow.OnEnter += (orig, self) => {
                    orig(self);
                    var newCrit = RollHypercrit(self.characterBody.critMultiplier - 2f, self.characterBody);
                    if (newCrit.numCrits > 1)
                        newCrit.damageMult *= 6 / (float)(3 + 3 * newCrit.numCrits);
                    critInfoAttachments.Add(self, newCrit);

                    self.isCrit = newCrit.numCrits > 0;
                    self.maxArrowCount = 3 + newCrit.numCrits * 3;
                    self.arrowReloadDuration = self.baseArrowReloadDuration * (3f / self.maxArrowCount) / self.attackSpeedStat;
                };
                IL.EntityStates.Huntress.HuntressWeapon.FireSeekingArrow.FireOrbArrow += (il) => {
                    var c = new ILCursor(il);
                    c.GotoNext(x => x.MatchStloc(0));
                    c.Emit(OpCodes.Dup);
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<GenericDamageOrb, EntityStates.Huntress.HuntressWeapon.FireSeekingArrow>>((orb, self) => {
                        TryPassHypercrit(self, orb);
                    });
                };
                IL.RoR2.Orbs.GenericDamageOrb.OnArrival += (il) => {
                    var c = new ILCursor(il);
                    c.GotoNext(MoveType.After,
                        x => x.MatchNewobj<DamageInfo>());
                    c.Emit(OpCodes.Dup);
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<DamageInfo, GenericDamageOrb>>((di, orb) => {
                        TryPassHypercrit(orb, di);
                    });
                };
            }
        }

        public static void BetterUICompat()
        {
            BetterUI.StatsDisplay.regexmap.Add("$hypercrit", statBody => GetDamage(statBody.crit, statBody.critMultiplier - 2).ToString("0.##"));
            var sortedKeys = BetterUI.StatsDisplay.regexmap.Keys.ToList();
            sortedKeys.Sort((s1, s2) => s2.Length - s1.Length);
            BetterUI.StatsDisplay.regexpattern = new Regex(@"(\" + string.Join(@"|\", sortedKeys) + ")");
        }

        public static void MoonglassesRework()
        {
            if (MysticsItems.ConfigManager.General.disabledItems.Keys.Any(x => ItemCatalog.GetItemDef(x).name == "MysticsItems_Moonglasses")) return;
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                if (self?.inventory != null)
                {
                    int count = self.inventory.GetItemCount(MysticsItems.MysticsItemsContent.Items.MysticsItems_Moonglasses);
                    if (count == 0) return;
                    self.crit *= Mathf.Pow(2, count);
                    self.crit -= Moonglasses.Value * count;
                    self.crit = Mathf.Max(0, self.crit);
                }
            };
            On.RoR2.Language.GetLocalizedStringByToken += (orig, self, token) =>
            {
                if (token == "ITEM_MYSTICSITEMS_MOONGLASSES_PICKUP") return orig(self, "ITEM_HYPERCRIT_MOONGLASSES_PICKUP");
                if (token == "ITEM_MYSTICSITEMS_MOONGLASSES_DESC") return orig(self, "ITEM_HYPERCRIT_MOONGLASSES_DESC");
                return orig(self, token);
            };
            LanguageAPI.Add("ITEM_HYPERCRIT_MOONGLASSES_PICKUP", "Increase Critical Strike damage... <color=#FF7F7F>BUT reduce Critical Strike chance.</color>");
            LanguageAPI.Add("ITEM_HYPERCRIT_MOONGLASSES_DESC", "Increase Critical Strike damage by <style=cIsDamage>{CritDamageIncrease}% <style=cStack>(+{CritDamageIncreasePerStack}% per stack)</style></style>. <style=cIsUtility>reduce Critical Strike chance by {0}% <style=cStack>(+{0}% per stack)</style> for each stack.</style>"
                .Replace("{0}", Moonglasses.Value.ToString())
                .Replace("{CritDamageIncrease}", MysticsItems.Items.Moonglasses.critDamageIncrease.ToString())
                .Replace("{CritDamageIncreasePerStack}", MysticsItems.Items.Moonglasses.critDamageIncreasePerStack.ToString()));
        }

        public static bool Mods(params string[] arr)
        {
            for (int i = 0; i < arr.Length; i++) if (!Chainloader.PluginInfos.ContainsKey(arr[i])) return false;
            return true;
        }

        //for mod interop
        public bool TryGetHypercrit(object target, ref AdditionalCritInfo aci)
        {
            return critInfoAttachments.TryGetValue(target, out aci);
        }

        private bool TryPassHypercrit(object from, object to)
        {
            bool retv = critInfoAttachments.TryGetValue(from, out AdditionalCritInfo aci);
            if (retv) critInfoAttachments.Add(to, aci);
            return retv;
        }

        private bool TryPassHypercrit(object from, object to, out AdditionalCritInfo aci)
        {
            bool retv = critInfoAttachments.TryGetValue(from, out aci);
            if (retv) critInfoAttachments.Add(to, aci);
            return retv;
        }

        public static AdditionalCritInfo RollHypercrit(float damage, CharacterBody body, bool forceSingleCrit = false)
        {
            var aci = new AdditionalCritInfo();
            if (body)
            {
                aci.totalCritChance = body.crit;
                //Base crit chance
                var bCrit = Mathf.Max(body.crit - (forceSingleCrit ? 100f : 0f), 0f);
                //Amount of non-guaranteed crit chance (for the final crit in the stack)
                var cCrit = bCrit % 100f;
                aci.numCrits = Mathf.FloorToInt(bCrit / 100f) + (Util.CheckRoll(cCrit, body.master) ? 1 : 0);
                if (Cap.Value >= 0) aci.numCrits = Mathf.Min(Cap.Value, aci.numCrits);
                if (forceSingleCrit) aci.numCrits++;
                if (aci.numCrits == 0) aci.damageMult = 1f;
                else aci.damageMult = Calc(Mode.Value, Base.Value + damage, Mult.Value + (DamageBonus.Value ? damage : 0), aci.numCrits);
                float proc = 0;
                if (aci.numCrits != 0) proc = Calc(ProcMode.Value, ProcBase.Value, ProcMult.Value, aci.numCrits);
                aci.numProcs = (int)proc + ((Run.instance.runRNG.RangeFloat(0, 1) < (proc % 1)) ? 1 : 0);
            }
            return aci;
        }

        public static float GetDamage(float chance, float damage)
        {
            int numCrits = Mathf.Min(1, Mathf.CeilToInt(chance / 100f));
            if (Cap.Value >= 0) numCrits = Mathf.Min(Cap.Value, numCrits);
            return Calc(Mode.Value, Base.Value + damage, Mult.Value + (DamageBonus.Value ? damage : 0), numCrits);
        }

        public static float Calc(CritStackingMode mode, float init, float mult, int count)
        {
            switch (mode)
            {
                case CritStackingMode.Linear:
                    return init + mult * (count - 1);
                case CritStackingMode.Exponential:
                    return init * Mathf.Pow(mult, count - 1);
                case CritStackingMode.Asymptotic:
                    return init + mult * (1f - Mathf.Pow(2, -(count - 1)));
            }
            Log.LogError("Invalid Mode??");
            return 0;
        }
    }
}
