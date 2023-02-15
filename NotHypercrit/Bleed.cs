using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace NotHypercrit
{
    public static class BleedExtension
    {
        public static float GetBleedDamage(this CharacterBody body)
        {
            if (!Bleed.bleedDamage.ContainsKey(body)) Bleed.bleedDamage.Add(body, 1);
            return Bleed.bleedDamage[body];
        }

        public static CharacterBody SetBleedDamage(this CharacterBody body, float value)
        {
            if (!Bleed.bleedDamage.ContainsKey(body)) Bleed.bleedDamage.Add(body, value);
            else Bleed.bleedDamage[body] = value;
            return body;
        }
        public static float GetCollapseDamage(this CharacterBody body)
        {
            if (!Bleed.collapseDamage.ContainsKey(body)) Bleed.collapseDamage.Add(body, 1);
            return Bleed.collapseDamage[body];
        }

        public static CharacterBody SetCollapseDamage(this CharacterBody body, float value)
        {
            if (!Bleed.collapseDamage.ContainsKey(body)) Bleed.collapseDamage.Add(body, value);
            else Bleed.collapseDamage[body] = value;
            return body;
        }
    }

    public class Bleed
    {
        public class InflictHyperbleedInfo : NotHypercritPlugin.AdditionalProcInfo
        {
            public GameObject attacker;
            public DotController.DotIndex dotIndex;
            public void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);
                dotIndex = (DotController.DotIndex)reader.ReadInt32();
            }
            public void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);
                writer.Write((int)dotIndex);
            }
        }


        public static List<InflictHyperbleedInfo> inflictHyperBleedInfos = new List<InflictHyperbleedInfo>();
        public static InflictHyperbleedInfo lastNetworkedBleedInfo = null;
        public static Dictionary<CharacterBody, float> bleedDamage = new();
        public static Dictionary<CharacterBody, float> collapseDamage = new();
        public static InflictDotInfo lastInfo;

        public static void Patch()
        {
            NotHypercritPlugin.Log.LogDebug("The Spirit of TheMysticSword Burns Alight...");
            Run.onRunStartGlobal += (run) => bleedDamage.Clear();
            On.RoR2.CharacterBody.Start += (orig, self) => { self.GetBleedDamage(); self.GetCollapseDamage(); orig(self); }; // initialize bleed damage so recalcs can use it
            RecalculateStatsAPI.GetStatCoefficients += (self, args) => { bleedDamage[self] = 1; collapseDamage[self] = 1; }; // ones that uses bleeddamage would have hypercrit2 as softdep so
            IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
            {
                ILCursor c = new(il);
                int damageInfoIndex = -1;
                if (
                    NotHypercritPlugin.BleedEnable.Value &&
                    c.TryGotoNext(x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.bleedChance))) &&
                    c.TryGotoNext(x => x.MatchLdcI4((int)ProcType.BleedOnHit)) &&
                    c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt<ProcChainMask>(nameof(ProcChainMask.AddProc)))
                )
                {
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate<Action<DamageInfo>>((info) =>
                    {
                        if (info.attacker)
                        {
                            CharacterBody self = info.attacker.GetComponent<CharacterBody>();
                            if (!self) return;
                            InflictHyperbleedInfo aci = RollHyperbleed(DotController.DotIndex.Bleed, self.GetBleedDamage() - 1f, self, false);
                            inflictHyperBleedInfos.Add(aci);
                        }
                    });
                }
                if (
                    NotHypercritPlugin.CollapseEnable.Value &&
                    c.TryGotoNext(x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.BleedOnHitVoid))) &&
                    c.TryGotoNext(x => x.MatchLdcI4((int)ProcType.FractureOnHit)) &&
                    c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt<ProcChainMask>(nameof(ProcChainMask.AddProc)))
                )
                {
                    c.Emit(OpCodes.Ldarg, 1);
                    c.EmitDelegate<Action<DamageInfo>>((info) =>
                    {
                        if (info.attacker)
                        {
                            CharacterBody self = info.attacker.GetComponent<CharacterBody>();
                            if (!self) return;
                            InflictHyperbleedInfo aci = RollHyperbleed(DotController.DotIndex.Fracture, self.GetCollapseDamage() - 1f, self, true);
                            inflictHyperBleedInfos.Add(aci);
                        }
                    });
                }
            };
            IL.RoR2.DotController.InflictDot_refInflictDotInfo += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchCallOrCallvirt<DotController.OnDotInflictedServerGlobalDelegate>(nameof(DotController.OnDotInflictedServerGlobalDelegate.Invoke)));
                c.GotoPrev(x => x.MatchLdarg(0));
                c.Remove();
                c.GotoNext(x => x.MatchCallOrCallvirt<DotController.OnDotInflictedServerGlobalDelegate>(nameof(DotController.OnDotInflictedServerGlobalDelegate.Invoke)));
                c.Remove();
                c.EmitDelegate<Action<DotController.OnDotInflictedServerGlobalDelegate, DotController>>((inflictedServerGlobal, self) =>
                {
                    var attacker = lastInfo.attackerObject;
                    var dotIndex = lastInfo.dotIndex;
                    if (attacker && ((dotIndex == DotController.DotIndex.Bleed && NotHypercritPlugin.BleedEnable.Value) || (dotIndex == DotController.DotIndex.Fracture && NotHypercritPlugin.CollapseEnable.Value)))
                    {
                        var inflictHyperbleedInfo = inflictHyperBleedInfos.Find(x => x.attacker == attacker && x.dotIndex == dotIndex);
                        if (inflictedServerGlobal != null) for (var i = 0; i < inflictHyperbleedInfo.numProcs; i++) inflictedServerGlobal(self, ref lastInfo);
                        inflictHyperBleedInfos.Remove(inflictHyperbleedInfo);
                    }
                });
            };
            if (NotHypercritPlugin.BleedEnable.Value && NotHypercritPlugin.BleedColor.Value > 1) IL.RoR2.HealthComponent.HandleDamageDealt += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchCallOrCallvirt<DamageNumberManager>(nameof(DamageNumberManager.SpawnDamageNumber)));
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Pop); // I got gun for fake necklace
                c.Emit(OpCodes.Pop); // gun that send texts
                c.Emit(OpCodes.Pop); // gun that make breakfast
                c.Emit(OpCodes.Pop); // gun that sign breasts
                c.Emit(OpCodes.Pop);
                c.Remove();
                c.Emit(OpCodes.Ldloc_0);
                c.Emit(OpCodes.Ldloc_2);
                c.EmitDelegate<Action<DamageDealtMessage, TeamComponent>>((msg, team) =>
                {
                    if (msg.damageColorIndex == DamageColorIndex.Bleed && (bool)msg.victim && (bool)msg.attacker)
                    {
                        CharacterBody attacker = msg.attacker.GetComponent<CharacterBody>();
                        CharacterBody victim = msg.victim.GetComponent<CharacterBody>();
                        if (attacker && victim)
                        {
                            // weird spawndamagenumber variant!!
                            float mult = msg.damage / (victim.GetBuffCount(RoR2Content.Buffs.Bleeding) * 0.2f * attacker.baseDamage) / NotHypercritPlugin.Calc(NotHypercritPlugin.BleedStackMode.Value, NotHypercritPlugin.BleedStackBase.Value, NotHypercritPlugin.BleedStackMult.Value, NotHypercritPlugin.BleedStackDecay.Value, victim.GetBuffCount(RoR2Content.Buffs.Bleeding));
                            float stack = GetInverseDamage(mult); // yeah
                            if (mult > 0 && stack > 0)
                            {
                                float v = (stack - 1) % NotHypercritPlugin.BleedColor.Value / (NotHypercritPlugin.BleedColor.Value - 1);
                                Color c = Color.HSVToRGB(0f, 142f / 240f, 0.75f - (0.5f * v));
                                if (team.teamIndex == TeamIndex.None) c *= Color.gray;
                                if (team.teamIndex == TeamIndex.Monster) c *= new Color(0.5568628f, 0.2941177f, 0.6039216f);
                                DamageNumberManager.instance.ps.Emit(new ParticleSystem.EmitParams() { position = msg.position, startColor = c, applyShapeToPosition = true }, 1);
                                DamageNumberManager.instance.ps.GetCustomParticleData(DamageNumberManager.instance.customData, ParticleSystemCustomData.Custom1);
                                DamageNumberManager.instance.customData[DamageNumberManager.instance.customData.Count - 1] = new Vector4(1f, 0.0f, msg.damage, msg.crit ? 1f : 0.0f);
                                DamageNumberManager.instance.ps.SetCustomParticleData(DamageNumberManager.instance.customData, ParticleSystemCustomData.Custom1);
                                return;
                            }
                        }
                    }
                    DamageNumberManager.instance.SpawnDamageNumber(msg.damage, msg.position, msg.crit, (bool)team ? team.teamIndex : TeamIndex.None, msg.damageColorIndex);
                });
            };
        }

        public static void PatchStack()
        {
            On.RoR2.DotController.InflictDot_refInflictDotInfo += (On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo info) =>
            {
                if (NetworkServer.active)
                {
                    var attacker = info.attackerObject;
                    var dotIndex = info.dotIndex;
                    if (attacker)
                    {
                        if ((info.dotIndex == DotController.DotIndex.Bleed && NotHypercritPlugin.BleedEnable.Value) || (info.dotIndex == DotController.DotIndex.Fracture && NotHypercritPlugin.CollapseEnable.Value)) info.damageMultiplier *= HyperbleedMultiplier(attacker, dotIndex);
                        if (info.victimObject != null)
                        {
                            var victim = info.victimObject.GetComponent<CharacterBody>();
                            if (victim != null)
                            {
                                if (info.dotIndex == DotController.DotIndex.Bleed) info.damageMultiplier *= NotHypercritPlugin.Calc(NotHypercritPlugin.BleedStackMode.Value, NotHypercritPlugin.BleedStackBase.Value, NotHypercritPlugin.BleedStackMult.Value, NotHypercritPlugin.BleedStackDecay.Value, victim.GetBuffCount(RoR2Content.Buffs.Bleeding));
                                else if (info.dotIndex == DotController.DotIndex.Fracture) info.damageMultiplier *= NotHypercritPlugin.Calc(NotHypercritPlugin.CollapseStackMode.Value, NotHypercritPlugin.CollapseStackBase.Value, NotHypercritPlugin.CollapseStackMult.Value, NotHypercritPlugin.CollapseStackDecay.Value, victim.GetBuffCount(DLC1Content.Buffs.Fracture));
                            }
                        }
                    }
                }
                lastInfo = info;
                orig(ref info);
            };
            if (NotHypercritPlugin.LamerShatterspleen.Value)
            {
                IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
                {
                    ILCursor c = new(il);
                    c.GotoNext(x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BleedOnHitAndExplode)), x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)));
                    c.Index += 2;
                    c.Emit(OpCodes.Pop);
                    c.Emit(OpCodes.Ldc_I4, 0);
                };
                On.RoR2.Language.GetLocalizedStringByToken += (orig, self, token) =>
                {
                    if (token == "ITEM_BLEEDONHITANDEXPLODE_DESC")
                    {
                        string ret = "Gain <style=cIsDamage>5%</style> critical chance. Gain <style=cIsDamage>Bleed chance</style> equal to your <style=cIsDamage>Critical chance</style>. <style=cIsDamage>Bleeding</style> enemies <style=cIsDamage>explode</style> on death for <style=cIsDamage>400%</style> <style=cStack>(+400% per stack)</style> damage";
                        if (!NotHypercritPlugin.Mods("Hayaku.VanillaRebalance")) ret += ", plus an additional <style=cIsDamage>15%</style> <style=cStack>(+15% per stack)</style> of their maximum health";
                        return ret + ".";
                    }
                    else return orig(self, token);
                };
            }
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                if (self == null || self.inventory == null) return;
                if (NotHypercritPlugin.LamerShatterspleen.Value && self.inventory.GetItemCount(RoR2Content.Items.BleedOnHitAndExplode) > 0) self.bleedChance += self.crit;
                if (NotHypercritPlugin.HyperbolicBleed.Value) self.bleedChance = 100f - (10000f / (100f + (1.111111f * self.bleedChance)));
            };
            if (NotHypercritPlugin.HyperbolicCollapse.Value) IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchLdloc(24), x => x.MatchConvR4());
                c.Index += 2;
                c.EmitDelegate<Func<float, float>>(chance =>
                {
                    return 10f - (10f / (1f + (0.011111f * chance)));
                });
            };
        }
        public static float HyperbleedMultiplier(GameObject attacker, DotController.DotIndex dotIndex)
        {
            var inflictHyperbleedInfo = inflictHyperBleedInfos.Find(x => x.attacker == attacker && x.dotIndex == dotIndex);
            if (!inflictHyperbleedInfo.Equals(default(InflictHyperbleedInfo))) return inflictHyperbleedInfo.damageMult; // this should cover mods that don't use hypercrit2?
            return 1;
        }

        public static InflictHyperbleedInfo RollHyperbleed(DotController.DotIndex dotIndex, float damage, CharacterBody body, bool forceSingleBleed = false)
        {
            var aci = new InflictHyperbleedInfo();
            if (body)
            {
                aci.attacker = body.gameObject;
                aci.dotIndex = dotIndex;
                aci.totalChance = body.bleedChance;
                float effectiveCount = GetEffectiveCount(body.bleedChance, body, dotIndex == DotController.DotIndex.Fracture, forceSingleBleed);
                aci.num = (dotIndex == DotController.DotIndex.Bleed ? NotHypercritPlugin.BleedFraction.Value : NotHypercritPlugin.CollapseFraction.Value) ? (int)effectiveCount : (Mathf.FloorToInt(effectiveCount) + (Util.CheckRoll(effectiveCount % 1f, body.master) ? 1 : 0));
                if (aci.num == 0) aci.damageMult = 1f;
                else
                {
                    if (dotIndex == DotController.DotIndex.Bleed) aci.damageMult = NotHypercritPlugin.Calc(
                    NotHypercritPlugin.BleedMode.Value,
                    NotHypercritPlugin.BleedBase.Value + damage,
                    NotHypercritPlugin.BleedMult.Value + (NotHypercritPlugin.BleedDamageBonus.Value ? damage : 0),
                    NotHypercritPlugin.BleedDecay.Value,
                    effectiveCount);
                    else if (dotIndex == DotController.DotIndex.Fracture) aci.damageMult = NotHypercritPlugin.Calc(
                    NotHypercritPlugin.CollapseMode.Value,
                    NotHypercritPlugin.CollapseBase.Value + damage,
                    NotHypercritPlugin.CollapseMult.Value + (NotHypercritPlugin.CollapseDamageBonus.Value ? damage : 0),
                    NotHypercritPlugin.CollapseDecay.Value,
                    effectiveCount);
                }
                float proc = 0;
                if (aci.num != 0)
                {
                    if (dotIndex == DotController.DotIndex.Bleed) proc = NotHypercritPlugin.Calc(
                    NotHypercritPlugin.BleedProcMode.Value,
                    NotHypercritPlugin.BleedProcBase.Value,
                    NotHypercritPlugin.BleedProcMult.Value,
                    NotHypercritPlugin.BleedProcDecay.Value,
                    effectiveCount);
                    else if (dotIndex == DotController.DotIndex.Fracture) proc = NotHypercritPlugin.Calc(
                    NotHypercritPlugin.CollapseProcMode.Value,
                    NotHypercritPlugin.CollapseProcBase.Value,
                    NotHypercritPlugin.CollapseProcMult.Value,
                    NotHypercritPlugin.CollapseProcDecay.Value,
                    effectiveCount);
                }
                aci.numProcs = (int)proc + ((Run.instance.runRNG.RangeFloat(0, 1) < (proc % 1)) ? 1 : 0);
            }
            return aci;
        }

        public static float GetDamage(float chance, float damage, CharacterBody body, bool collapse = false)
        {
            if (!collapse) return NotHypercritPlugin.Calc(
                NotHypercritPlugin.BleedMode.Value,
                NotHypercritPlugin.BleedBase.Value + damage,
                NotHypercritPlugin.BleedMult.Value + (NotHypercritPlugin.CritDamageBonus.Value ? damage : 0),
                NotHypercritPlugin.BleedDecay.Value,
                Mathf.Max(1, GetEffectiveCount(chance + (100 - (chance % 100f)), body)));
            else return NotHypercritPlugin.Calc(
                NotHypercritPlugin.CollapseMode.Value,
                NotHypercritPlugin.CollapseBase.Value + damage,
                NotHypercritPlugin.CollapseMult.Value + (NotHypercritPlugin.CollapseDamageBonus.Value ? damage : 0),
                NotHypercritPlugin.CollapseDecay.Value,
                Mathf.Max(1, GetEffectiveCount(chance + (100 - (chance % 100f)), body, true)));
        }

        public static float GetEffectiveCount(InflictHyperbleedInfo aci)
        {
            if (NotHypercritPlugin.BleedFraction.Value)
            {
                float ret = aci.totalChance / 100f;
                if (ret < 1) ret = 0;
                return ret;
            }
            else return aci.num;
        }

        public static float GetEffectiveCount(float chance, CharacterBody body, bool collapse = false, bool forceSingleBleed = false)
        {
            float ret = Mathf.Max(0, (chance - (forceSingleBleed ? 100 : 0)) / 100f);
            if ((collapse ? NotHypercritPlugin.CollapseCap.Value : NotHypercritPlugin.BleedCap.Value) >= 0) ret = Mathf.Min(ret, collapse ? NotHypercritPlugin.CollapseCap.Value : NotHypercritPlugin.BleedCap.Value);
            if (((NotHypercritPlugin.BleedFraction.Value && !collapse) || (NotHypercritPlugin.CollapseFraction.Value && collapse)) && NotHypercritPlugin.Mods("com.themysticsword.mysticsitems") && body?.inventory != null && ret > 0) ret += 0.01f * body.inventory.GetItemCount(ItemCatalog.FindItemIndex("MysticsItems_ScratchTicket"));
            if (ret <= 0) return 0;
            if (!(collapse ? NotHypercritPlugin.CollapseFraction.Value : NotHypercritPlugin.BleedFraction.Value)) return Mathf.FloorToInt(ret) + (Util.CheckRoll(ret % 1f, body.master) ? 1 : 0);
            return ret;
        }

        public static float GetInverseDamage(float mult) // bleed only
        {
            switch (NotHypercritPlugin.BleedMode.Value)
            {
                case NotHypercritPlugin.CritStackingMode.Linear:
                    if (NotHypercritPlugin.BleedMult.Value == 0) return 0;
                    return (mult - NotHypercritPlugin.BleedBase.Value) / NotHypercritPlugin.BleedMult.Value + 1;
                case NotHypercritPlugin.CritStackingMode.Exponential:
                    if (NotHypercritPlugin.BleedBase.Value == 0 || NotHypercritPlugin.BleedMult.Value == 1 || NotHypercritPlugin.BleedMult.Value <= 0) return 0;
                    return Mathf.Log(mult / NotHypercritPlugin.BleedBase.Value, NotHypercritPlugin.BleedMult.Value) + 1;
                case NotHypercritPlugin.CritStackingMode.Hyperbolic:
                    if (NotHypercritPlugin.BleedBase.Value == mult || ((NotHypercritPlugin.BleedMult.Value * NotHypercritPlugin.BleedDecay.Value / (mult - NotHypercritPlugin.BleedBase.Value)) - NotHypercritPlugin.BleedDecay.Value) == 0) return 0;
                    return (NotHypercritPlugin.BleedMult.Value - NotHypercritPlugin.BleedDecay.Value) / ((NotHypercritPlugin.BleedMult.Value * NotHypercritPlugin.BleedDecay.Value / (mult - NotHypercritPlugin.BleedBase.Value)) - NotHypercritPlugin.BleedDecay.Value);
                case NotHypercritPlugin.CritStackingMode.Asymptotic:
                    if (NotHypercritPlugin.BleedMult.Value == 0) return 0;
                    return -Mathf.Log(1 - ((mult - NotHypercritPlugin.BleedBase.Value) / NotHypercritPlugin.BleedMult.Value), 2) * NotHypercritPlugin.BleedDecay.Value + 1;
            }
            NotHypercritPlugin.Log.LogError("Invalid Mode??");
            return 0;
        }
    }
}
