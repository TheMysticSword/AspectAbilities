using UnityEngine;
using RoR2;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;
using R2API.Utils;

namespace TheMysticSword.AspectAbilities.Buffs
{
    public class IceCrystalDebuff : BaseBuff
    {
        public override void OnLoad()
        {
            buffDef.name = "IceCrystalDebuff";
            buffDef.buffColor = AffixWhite.iceCrystalColor;
            buffDef.canStack = false;
            buffDef.iconSprite = Resources.Load<Sprite>("Textures/BuffIcons/texBuffPulverizeIcon");
            buffDef.isDebuff = true;

            NetworkingAPI.RegisterMessageType<CurseCount.SyncStack>();

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<CurseCount>();
            };

            IL.RoR2.CharacterBody.RecalculateStats += (il) =>
            {
                ILCursor c = new ILCursor(il);
                int maxHealthPrevPos = 48;
                int maxShieldPrevPos = 49;
                int trueMaxHealthPos = 50;
                int trueMaxShieldPos = 52;
                int permaCurseBuffCountPos = 78;
                int maxHealthDeltaPos = 80;
                int maxShieldDeltaPos = 81;
                // increase curse penalty
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcR4(1),
                    x => x.MatchCallOrCallvirt<CharacterBody>("set_cursePenalty")
                );
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchCallOrCallvirt<CharacterBody>("GetBuffCount"),
                    x => x.MatchStloc(permaCurseBuffCountPos)
                );
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<System.Action<CharacterBody>>((characterBody) =>
                {
                    CurseCount curseCount = characterBody.GetComponent<CurseCount>();
                    if (curseCount.current > 0)
                    {
                        // need to use this here instead of characterBody.InvokeMethod - Rein's Sniper subclasses CharacterBody and InvokeMethod can't find the property on that new subclass
                        typeof(CharacterBody).GetProperty("cursePenalty").SetValue(characterBody, characterBody.cursePenalty + curseCount.current);
                    }
                });
                // don't regen health when this curse penalty is removed
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_maxHealth"),
                    x => x.MatchLdloc(maxHealthPrevPos),
                    x => x.MatchSub(),
                    x => x.MatchStloc(maxHealthDeltaPos)
                );
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, trueMaxHealthPos);
                c.Emit(OpCodes.Ldloc, maxHealthDeltaPos);
                c.EmitDelegate<System.Func<CharacterBody, float, float, float>>((characterBody, trueMaxHealth, maxHealthDelta) =>
                {
                    CurseCount curseCount = characterBody.GetComponent<CurseCount>();
                    float healthGained = (trueMaxHealth / (1f + curseCount.current)) - (trueMaxHealth / (1f + curseCount.last));
                    if (healthGained > 0)
                    {
                        maxHealthDelta -= healthGained;
                    }
                    else if (healthGained < 0)
                    {
                        float takeDamage = -healthGained * (characterBody.healthComponent.Networkhealth / (trueMaxHealth / (1f + curseCount.last)));
                        // don't reduce below 1
                        if (takeDamage > characterBody.healthComponent.Networkhealth)
                        {
                            takeDamage = characterBody.healthComponent.Networkhealth - 1f;
                        }
                        if (takeDamage > 0) characterBody.healthComponent.Networkhealth -= takeDamage;
                    }
                    return maxHealthDelta;
                });
                c.Emit(OpCodes.Stloc, maxHealthDeltaPos);
                // do the same with shields
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_maxShield"),
                    x => x.MatchLdloc(maxShieldPrevPos),
                    x => x.MatchSub(),
                    x => x.MatchStloc(maxShieldDeltaPos)
                );
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, trueMaxShieldPos);
                c.Emit(OpCodes.Ldloc, maxShieldDeltaPos);
                c.EmitDelegate<System.Func<CharacterBody, float, float, float>>((characterBody, trueMaxHealth, maxHealthDelta) =>
                {
                    CurseCount curseCount = characterBody.GetComponent<CurseCount>();
                    float healthGained = (trueMaxHealth / (1f + curseCount.current)) - (trueMaxHealth / (1f + curseCount.last));
                    if (healthGained > 0)
                    {
                        maxHealthDelta -= healthGained;
                    }
                    else if (healthGained < 0)
                    {
                        float takeDamage = -healthGained * (characterBody.healthComponent.Networkshield / (trueMaxHealth / (1f + curseCount.last)));
                        // don't reduce below 1
                        if (takeDamage > characterBody.healthComponent.Networkshield)
                        {
                            takeDamage = characterBody.healthComponent.Networkshield - 1f;
                        }
                        if (takeDamage > 0) characterBody.healthComponent.Networkshield -= takeDamage;
                    }
                    return maxHealthDelta;
                });
                c.Emit(OpCodes.Stloc, maxShieldDeltaPos);
            };
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                CurseCount curseCount = self.GetComponent<CurseCount>();
                curseCount.last = curseCount.current;
            };
            /*
             * normally, the curse value is reduced by the CurseCount instance
             * however, if the buff gets manually removed (e.g. Blast Shower), nothing happens because the curse value gets re-set by CurseCount on the next frame
             * that's why we need to manually reduce the curse count when all debuff stacks are lost
             */
            On.RoR2.CharacterBody.OnBuffFinalStackLost += (orig, self, buffDef2) =>
            {
                orig(self, buffDef2);
                if (buffDef2 == buffDef)
                {
                    CurseCount curseCount = self.GetComponent<CurseCount>();
                    if (curseCount.current > 0f)
                    {
                        curseCount.current = 0f;
                        self.SetFieldValue("statsDirty", true);
                    }
                }
            };
        }

        public class CurseCount : NetworkBehaviour
        {
            public CharacterBody characterBody;
            public float current = 0f;
            public float last = 0f;
            public float decayTotal = 0f;
            public float decayTime = 3f;
            public float noDecayTime = 0f;
            public float updateStatsTime = 0f;
            public float updateStatsTimeMax = AffixWhite.cursePenaltyStackFrequency;
            public bool canStackNow = true;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
            }

            public void Stack(float count, float noDecayTime)
            {
                if (canStackNow)
                {
                    canStackNow = false;
                    current += count;
                    this.noDecayTime = noDecayTime + 0.1f;
                    decayTotal = current;
                    characterBody.SetFieldValue("outOfDangerStopwatch", 0f);
                    characterBody.SetFieldValue("statsDirty", true);

                    if (NetworkServer.active)
                    {
                        characterBody.AddTimedBuff(AspectAbilitiesContent.Buffs.IceCrystalDebuff, decayTime);
                        new SyncStack(gameObject.GetComponent<NetworkIdentity>().netId, count, noDecayTime).Send(NetworkDestination.Clients);
                    }
                }
            }

            public class SyncStack : INetMessage
            {
                NetworkInstanceId objID;
                float count;
                float noDecayTime;

                public SyncStack()
                {
                }

                public SyncStack(NetworkInstanceId objID, float count, float noDecayTime)
                {
                    this.objID = objID;
                    this.count = count;
                    this.noDecayTime = noDecayTime;
                }

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    count = reader.ReadSingle();
                    noDecayTime = reader.ReadSingle();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active) return;
                    GameObject obj = Util.FindNetworkObject(objID);
                    if (!obj) return;
                    CurseCount curseCount = obj.GetComponent<CurseCount>();
                    curseCount.Stack(count, noDecayTime);
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(count);
                    writer.Write(noDecayTime);
                }
            }

            public void FixedUpdate()
            {
                updateStatsTime += Time.fixedDeltaTime;
                if (updateStatsTime >= updateStatsTimeMax)
                {
                    canStackNow = true;
                    updateStatsTime = 0f;
                }

                noDecayTime -= Time.fixedDeltaTime;
                if (noDecayTime <= 0 && current > 0f)
                {
                    current -= (decayTotal / decayTime) * Time.fixedDeltaTime;
                    if (updateStatsTime == 0f) characterBody.SetFieldValue("statsDirty", true);
                    if (current < 0f)
                    {
                        characterBody.SetFieldValue("statsDirty", true);
                        current = 0f;
                        if (NetworkServer.active && characterBody.HasBuff(AspectAbilitiesContent.Buffs.IceCrystalDebuff)) characterBody.RemoveBuff(AspectAbilitiesContent.Buffs.IceCrystalDebuff);
                    }
                }
            }
        }
    }
}
