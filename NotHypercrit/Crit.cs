using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Orbs;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using HarmonyLib;

namespace NotHypercrit
{
    public class Crit
    {
        public static ConditionalWeakTable<object, Main.AdditionalProcInfo> critInfoAttachments = new ConditionalWeakTable<object, Main.AdditionalProcInfo>();
        public static Main.AdditionalProcInfo lastNetworkedCritInfo = null;
        public static void Patch()
        {
            Main.Log.LogDebug("The Spirit of ThinkInvis Embraces You...");
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
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.critMultiplier))) && damageInfoIndex != -1)
                {
                    c.Emit(OpCodes.Ldloc_1);
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldarg, damageInfoIndex);
                    c.EmitDelegate<Func<float, CharacterBody, HealthComponent, DamageInfo, float>>((orig, self, self2, info) =>
                    {
                        if (!self) return orig;
                        Main.AdditionalProcInfo aci = null;
                        if (!critInfoAttachments.TryGetValue(info, out aci))
                        {
                            aci = RollHypercrit(orig - 2f, self, self2.body);
                            critInfoAttachments.Add(info, aci);
                        }
                        return Mathf.Max(orig, aci.damageMult); // jank 2, thanks railr
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
                Main.AdditionalProcInfo aci;
                if (!critInfoAttachments.TryGetValue(self, out aci)) aci = new();
                aci.Serialize(writer);
            };
            On.RoR2.DamageDealtMessage.Deserialize += (orig, self, reader) => {
                orig(self, reader);
                Main.AdditionalProcInfo aci = new();
                aci.Deserialize(reader);
                critInfoAttachments.Add(self, aci);
                lastNetworkedCritInfo = aci;
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
            IL.RoR2.DamageNumberManager.SpawnDamageNumber += (il) => {
                var c = new ILCursor(il);
                c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(DamageColor), nameof(DamageColor.FindColor)));
                c.EmitDelegate<Func<Color, Color>>((origColor) => {
                    if (lastNetworkedCritInfo == null) return origColor;
                    var aci = lastNetworkedCritInfo;
                    lastNetworkedCritInfo = null;
                    float effectiveCount = GetEffectiveCount(aci);
                    if (effectiveCount == 0) return origColor;
                    float h = 1f / 6f - (effectiveCount - 1f) / Main.CritColor.Value;
                    return Color.HSVToRGB(((h % 1f) + 1f) % 1f, 1f, 1f);
                });
            };
            if (Main.Flurry.Value)
            {
                On.EntityStates.Huntress.HuntressWeapon.FireFlurrySeekingArrow.OnEnter += (orig, self) => {
                    orig(self);
                    var newCrit = RollHypercrit(self.characterBody.critMultiplier - 2f, self.characterBody);
                    if (newCrit.num > 1)
                        newCrit.damageMult *= 6 / (float)(3 + 3 * newCrit.num);
                    critInfoAttachments.Add(self, newCrit);

                    self.isCrit = newCrit.num > 0;
                    self.maxArrowCount = 3 + newCrit.num * 3;
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

        public static void PatchHyperbolic()
        {
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                if (self == null || self.inventory == null) return;
                if (Main.HyperbolicCrit.Value) self.crit = 100f - (10000f / (100f + (1.111111f * self.crit)));
            };
        }

        public static void LaserScopeRework()
        {
            RecalculateStatsAPI.GetStatCoefficients += (self, args) =>
            {
                if (self == null || self.inventory == null) return;
                if (self.inventory.GetItemCount(DLC1Content.Items.CritDamage) > 0) args.critAdd += Main.LaserScope.Value;
            };
            On.RoR2.Language.GetLocalizedStringByToken += (orig, self, token) =>
            {
                if (token == "ITEM_CRITDAMAGE_DESC") return $"Gain <style=cIsDamage>{Main.LaserScope.Value}% critical chance</style>. " + orig(self, token);
                else return orig(self, token);
            };
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
                    self.crit -= Main.Moonglasses.Value * count;
                    if (self.crit < 0 && self.inventory.GetItemCount(DLC1Content.Items.ConvertCritChanceToCritDamage) > 0)
                    {
                        Chat.AddMessage("Hypercrit makes crit chance past 100 give a chance to do a hit that is your crit damage +100%. So in a scenario where your crit is 101% and crit damage is x2, mean your actual crit damage will be x2 in 100% cases and x3 in 1% cases. So to put it simply from x2 to x3.\r\nNow negative crit would work like this. Let's say you have -50% chance and x3.5 crit damage. Obviously no way for you to proc crits randomly, but could give a change to deal \"negative crit\" in other ways, that is -100% from you crit damage for every -100% crit chance past 0. So your crit damage range would be from x2.5 to x3.5.\r\nAt the least I was hoping that negative would work with survivors that can't have chance and instead get it converted to crit damage. Like railgunner.");
                        self.master.TrueKill();
                    }
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
                .Replace("{0}", Main.Moonglasses.Value.ToString())
                .Replace("{CritDamageIncrease}", MysticsItems.Items.Moonglasses.critDamageIncrease.ToString())
                .Replace("{CritDamageIncreasePerStack}", MysticsItems.Items.Moonglasses.critDamageIncreasePerStack.ToString()));
        }

        //for mod interop
        public static bool TryGetHypercrit(object target, ref Main.AdditionalProcInfo aci)
        {
            return critInfoAttachments.TryGetValue(target, out aci);
        }

        private static bool TryPassHypercrit(object from, object to)
        {
            bool retv = critInfoAttachments.TryGetValue(from, out Main.AdditionalProcInfo aci);
            if (retv) critInfoAttachments.Add(to, aci);
            return retv;
        }

        private static bool TryPassHypercrit(object from, object to, out Main.AdditionalProcInfo aci)
        {
            bool retv = critInfoAttachments.TryGetValue(from, out aci);
            if (retv) critInfoAttachments.Add(to, aci);
            return retv;
        }

        public static Main.AdditionalProcInfo RollHypercrit(float damage, CharacterBody body, CharacterBody body2 = null)
        {
            var aci = new Main.AdditionalProcInfo();
            if (body)
            {
                aci.totalChance = body.crit;
                float effectiveCount = GetEffectiveCount(body.crit, body);
                if (body2 != null && Main.Mods("com.TeamMoonstorm.Starstorm2-Nightly") && NeedlesCompat(body2)) effectiveCount++;
                aci.num = Main.CritFraction.Value ? (Mathf.FloorToInt(effectiveCount) + (Util.CheckRoll(effectiveCount % 1f * 100, body.master) ? 1 : 0)) : (int)effectiveCount;
                if (aci.num == 0) aci.damageMult = 1f;
                else aci.damageMult = Main.Calc(
                    Main.CritMode.Value, 
                    Main.CritBase.Value + damage, 
                    Main.CritMult.Value + (Main.CritDamageBonus.Value ? damage : 0), 
                    Main.CritDecay.Value, 
                    effectiveCount);
                float proc = 0;
                if (aci.num != 0) proc = Main.Calc(
                    Main.CritProcMode.Value, 
                    Main.CritProcBase.Value, 
                    Main.CritProcMult.Value, 
                    Main.CritProcDecay.Value, 
                    effectiveCount);
                aci.numProcs = (int)proc + (Util.CheckRoll(proc % 1 * 100) ? 1 : 0);
            }
            return aci;
        }

        public static float GetDamage(float chance, float damage, CharacterBody body)
        {
            return Main.Calc(
                Main.CritMode.Value, 
                Main.CritBase.Value + damage, 
                Main.CritMult.Value + (Main.CritDamageBonus.Value ? damage : 0), 
                Main.CritDecay.Value, 
                Mathf.Max(1, GetEffectiveCount(chance + (100 - (chance % 100f)), body)));
        }

        public static float GetEffectiveCount(Main.AdditionalProcInfo aci)
        {
            if (Main.CritFraction.Value)
            {
                float ret = aci.totalChance / 100f;
                if (ret < 1) ret = 0;
                return ret;
            }
            else return aci.num;
        }

        public static float GetEffectiveCount(float chance, CharacterBody body, bool forceSingleCrit = false)
        {
            float ret = Mathf.Max(0, (chance - (forceSingleCrit ? 100 : 0)) / 100f);
            if (Main.CritCap.Value >= 0) ret = Mathf.Min(ret, Main.CritCap.Value);
            if (Main.CritFraction.Value && Main.Mods("com.themysticsword.mysticsitems") && body?.inventory != null && ret > 0) ret += 0.01f * body.inventory.GetItemCount(ItemCatalog.FindItemIndex("MysticsItems_ScratchTicket"));
            if (ret <= 0) return 0;
            if (!Main.CritFraction.Value) return Mathf.FloorToInt(ret) + (Util.CheckRoll(ret % 1f * 100, body.master) ? 1 : 0);
            return ret;
        }

        public static bool NeedlesCompat(CharacterBody body)
        {
            return body.HasBuff(Moonstorm.Starstorm2.SS2Content.Buffs.BuffNeedle);
        }
    }
}
