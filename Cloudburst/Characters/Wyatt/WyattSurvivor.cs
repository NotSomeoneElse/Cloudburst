﻿using BepInEx.Configuration;
using Cloudburst.Wyatt.Components;
using Cloudburst.GlobalComponents;
using Cloudburst.Modules;
using Cloudburst.Modules.Characters;
using Cloudburst.Modules.Survivors;
using EntityStates;
using RoR2;
using RoR2.Skills;
using System;
using System.Collections.Generic;
using UnityEngine;
using Cloudburst.CEntityStates.Wyatt;

namespace Cloudburst.Characters.Wyatt
{
    public class WyattSurvivor : SurvivorBase<WyattSurvivor>
    {
        public override string prefabBodyName => "Wyatt";

        public const string WYATT_PREFIX = "CLOUDBURST_WYATT_";
        public override string survivorTokenPrefix => WYATT_PREFIX;

        public override BodyInfo bodyInfo { get; set; } = new BodyInfo
        {
            bodyName = "WyattBody",
            bodyNameToken = WYATT_PREFIX + "NAME",
            subtitleNameToken = WYATT_PREFIX + "SUBTITLE",

            characterPortrait = Assets.LoadAsset<Texture>("texIconWyatt"),
            bodyColor = Color.white,

            crosshair = Assets.LoadCrosshair("Standard"),
            podPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/SurvivorPod"),

            maxHealth = 110f,
            healthRegen = 1.5f,
            armor = 20f,

            jumpCount = 1,

            cameraParamsDepth = -10,
            cameraPivotPosition = new Vector3(0, 1, 0)
        };

        public override CustomRendererInfo[] customRendererInfos => new CustomRendererInfo[]
        {
                new CustomRendererInfo
                {
                    childName = "WyattMesh",
                },
                new CustomRendererInfo
                {
                    childName = "WyattBroom",
                }
        };

        public override UnlockableDef characterUnlockableDef => null;

        public override Type characterMainState => typeof(GenericCharacterMain);

        public override ItemDisplaysBase itemDisplays => new WyattItemDisplays();

        private static UnlockableDef masterySkinUnlockableDef;

        public BuffDef wyattGrooveBuffDef;
        public BuffDef wyattFlowBuffDef;
        public BuffDef wyattAntiGravBuffDef;

        public static SerializableEntityStateType DeployMaidState = new SerializableEntityStateType(typeof(DeployMaid));


        public override void Initialize()
        {
            characterEnabledConfig =
                Config.BindAndOptions(
                    "Survivors",
                    "Enable Custodian",
                    true,
                    "Set false to disable Custodian and as much of his content/code as possible",
                    true);

            base.Initialize();
        }
        
        protected override void OnCharacterInitialized()
        {
            WyattConfig.Init();
            WyattEffects.OnLoaded();
            WyattAssets.InitAss();
            WyattDamageTypes.InitDamageTypes();
            WyattLanguageTokens.AddLanguageTokens(WYATT_PREFIX);
            CreateBuffs();

            WyattEntityStates.AddEntityStates();

            bodyPrefab.AddComponent<WyattMAIDManager>();
            bodyPrefab.AddComponent<WyattNetworkCombat>();

            SfxLocator sfxLocator = bodyPrefab.GetComponent<SfxLocator>();
            //sfx
            sfxLocator.fallDamageSound = "Play_MULT_shift_hit";
            sfxLocator.landingSound = "play_char_land";

            //CharacterDeathBehavior characterDeathBehavior = wyattBody.GetComponent<CharacterDeathBehavior>();
            bodyPrefab.AddComponent<WyattWalkmanBehavior>();
            //kil
            //Cloudburst.Content.ContentHandler.Loadouts.RegisterEntityState(typeof(DeathState));
            //characterDeathBehavior.deathState = new SerializableEntityStateType(typeof(DeathState));

            GenericCursorEnemyTracker tracker = bodyPrefab.AddComponent<GenericCursorEnemyTracker>();

            GameObject indicatorPrefab = R2API.PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/EngiShieldRetractIndicator"), "WyattTrackerIndicator", false);
            SpriteRenderer indicator = indicatorPrefab.transform.Find("Holder").GetComponent<SpriteRenderer>(); //
            indicator.sprite = Assets.LoadAsset<Sprite>("texWyattIndicator");
            indicator.color = CCUtilities.HexToColor("00A86B");

            tracker.maxTrackingAngle = 20;
            tracker.maxTrackingDistance = 55;
            tracker.trackerUpdateFrequency = 5;
            tracker.indicatorPrefab = indicatorPrefab;

            AlterStatemachines();

            SetHooks();
        }

        #region hooks
        private void SetHooks()
        {
            R2API.RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;

            On.RoR2.CharacterBody.OnBuffFinalStackLost += CharacterBody_OnBuffFinalStackLost;
            On.RoR2.CharacterBody.OnBuffFirstStackGained += CharacterBody_OnBuffFirstStackGained;
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;

            On.RoR2.Projectile.BoomerangProjectile.FixedUpdate += BoomerangProjectile_FixedUpdate;
        }

        private void BoomerangProjectile_FixedUpdate(On.RoR2.Projectile.BoomerangProjectile.orig_FixedUpdate orig, RoR2.Projectile.BoomerangProjectile self)
        {
            orig(self);
            //Log.Warning($"state: {self.boomerangState} newtork: {self.NetworkboomerangState}");
        }

        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if(R2API.DamageAPI.HasModdedDamageType(damageInfo, WyattDamageTypes.antiGravDamage))
            {
                self.body.AddTimedBuff(wyattAntiGravBuffDef, WyattConfig.M1AntiGravDuration.Value);// 1.5f);
            }
            if (R2API.DamageAPI.HasModdedDamageType(damageInfo, WyattDamageTypes.antiGravDamage2))
            {
                self.body.AddTimedBuff(wyattAntiGravBuffDef, WyattConfig.M4SlamAntiGravDuration.Value);// 3f);
            }
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, R2API.RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(wyattGrooveBuffDef))
            {
                args.moveSpeedMultAdd += /*0.3f*/WyattConfig.M3GrooveSpeedMultiplierPerStack.Value * sender.GetBuffCount(wyattGrooveBuffDef);
                //args.armorAdd += 20;
            }

            if (sender.HasBuff(wyattFlowBuffDef))
            {
                args.cooldownMultAdd -= WyattConfig.M3FlowCDR.Value;// 0.3f;
                args.armorAdd += WyattConfig.M3FlowArmorBase.Value + WyattConfig.M3FlowArmorPerStack.Value * sender.GetBuffCount(wyattGrooveBuffDef);
            }

            if (sender.HasBuff(wyattAntiGravBuffDef))
            {
                args.attackSpeedMultAdd -= 0.5f;
                args.moveSpeedMultAdd -= 0.5f;
            }
        }

        private void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            if (self && self.HasBuff(wyattFlowBuffDef))
            {
                self.maxJumpCount+= WyattConfig.M3FlowExtraJumps.Value;
            }

            //if (self & self.HasBuff(wyattAntiGravBuffDef))
            //{
            //    if (self.characterMotor)
            //    {
            //        self.characterMotor.useGravity = false;
            //    }
            //}
        }

        private void CharacterBody_OnBuffFinalStackLost(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);
            if (buffDef == wyattAntiGravBuffDef)
            {
                if (self.characterMotor)
                {
                    self.characterMotor.useGravity = true;
                }
            }
        }

        private void CharacterBody_OnBuffFirstStackGained(On.RoR2.CharacterBody.orig_OnBuffFirstStackGained orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);
            if (buffDef == wyattAntiGravBuffDef)
            {
                if (self.characterMotor)
                {
                    self.characterMotor.useGravity = false;
                }
            }
        }
#endregion hooks

        private void CreateBuffs()
        {
            wyattGrooveBuffDef = Buffs.AddNewBuff(
                "CloudburstWyattCombatBuff",
                Assets.LoadAsset<Sprite>("WyattVelocity"),
                new Color(1f, 0.7882353f, 0.05490196f),
                true,
                false);

            wyattFlowBuffDef = Buffs.AddNewBuff(
                "CloudburstWyattFlowBuff",
                Assets.LoadAsset<Sprite>("WyattVelocity"),
                CCUtilities.HexToColor("#37323e"),
                false,
                false);

            wyattAntiGravBuffDef = Buffs.AddNewBuff(
                "CloudburstWyattAntiGravBuff",
                Assets.LoadAsset<Sprite>("texIconBuffAntiGrav"),
                new Color(0.6784314f, 0.6117647f, 0.4117647f),
                false,
                true);
        }

        public void AlterStatemachines()
        {
            SetStateOnHurt setStateOnHurt = bodyPrefab.GetComponent<SetStateOnHurt>();
            NetworkStateMachine networkStateMachine = bodyPrefab.GetComponent<NetworkStateMachine>();

            EntityStateMachine maidMachine = bodyPrefab.AddComponent<EntityStateMachine>();
            maidMachine.customName = "MAID";
            maidMachine.initialStateType = new SerializableEntityStateType(typeof(Idle));
            maidMachine.mainStateType = new SerializableEntityStateType(typeof(Idle));

            int idleLength = setStateOnHurt.idleStateMachine.Length;
            Array.Resize(ref setStateOnHurt.idleStateMachine, idleLength + 1);
            setStateOnHurt.idleStateMachine[idleLength] = maidMachine;

            int networkStateMachinesLength = networkStateMachine.stateMachines.Length;
            Array.Resize(ref networkStateMachine.stateMachines, networkStateMachinesLength + 1);
            networkStateMachine.stateMachines[networkStateMachinesLength] = maidMachine;


            EntityStateMachine marioJumpMachine = bodyPrefab.AddComponent<EntityStateMachine>();
            marioJumpMachine.customName = "SuperMarioJump";
            marioJumpMachine.initialStateType = new SerializableEntityStateType(typeof(Idle));
            marioJumpMachine.mainStateType = new SerializableEntityStateType(typeof(Idle));

            idleLength = setStateOnHurt.idleStateMachine.Length;
            Array.Resize(ref setStateOnHurt.idleStateMachine, idleLength + 1);
            setStateOnHurt.idleStateMachine[idleLength] = marioJumpMachine;

            networkStateMachinesLength = networkStateMachine.stateMachines.Length;
            Array.Resize(ref networkStateMachine.stateMachines, networkStateMachinesLength + 1);
            networkStateMachine.stateMachines[networkStateMachinesLength] = marioJumpMachine;
        }

        public override void InitializeUnlockables()
        {
            //uncomment this when you have a mastery skin. when you do, make sure you have an icon too
            //masterySkinUnlockableDef = Modules.Unlockables.AddUnlockable<Modules.Achievements.MasteryAchievement>();
        }

        public override void InitializeHitboxes()
        {
            ChildLocator childLocator = bodyPrefab.GetComponentInChildren<ChildLocator>();

            Prefabs.SetupHitbox(prefabCharacterModel.gameObject, childLocator.FindChild("TempHitbox"), "TempHitbox");
            Prefabs.SetupHitbox(prefabCharacterModel.gameObject, childLocator.FindChild("TempHitboxLarge"), "TempHitboxLarge");
            Prefabs.SetupHitbox(prefabCharacterModel.gameObject, childLocator.FindChild("TempHitboxSquish"), "TempHitboxSquish");
            Prefabs.SetupHitbox(prefabCharacterModel.gameObject, childLocator.FindChild("TempHitboxLunge"), "TempHitboxLunge");
        }

        #region skills
        public override void InitializeSkills()
        {
            Skills.CreateSkillFamilies(bodyPrefab);

            InitializePassive();

            InitializePrimarySkills();

            InitializeSecondarySkills();

            InitializeUtilitySkills();

            InitializeSpecialSkills();
        }

        private void InitializePassive()
        {
            SkillLocator.PassiveSkill passive = new SkillLocator.PassiveSkill
            {
                enabled = true,
                skillNameToken = "WYATT_PASSIVE_NAME",
                skillDescriptionToken = "WYATT_PASSIVE_DESCRIPTION",
                //keywordToken = "KEYWORD_VELOCITY",
                icon = Assets.LoadAsset<Sprite>("texIconWyattPassive")
            };

            //R2API.LanguageAPI.Add(passive.keywordToken, "<style=cKeywordName>Groove</style><style=cSub>Increases movement speed by X%.</style>");
            R2API.LanguageAPI.Add(passive.skillNameToken, "Walkman");
            R2API.LanguageAPI.Add(passive.skillDescriptionToken, "On hit, gain a stack of <style=cIsUtility>Groove</style>, granting <style=cIsUtility>30% move speed</style> per stack. Diminishes out of combat.");

            bodyPrefab.GetComponent<SkillLocator>().passiveSkill = passive;
        }

        private void InitializePrimarySkills()
        {
            //Creates a skilldef for a typical primary 
            SteppedSkillDef primarySkillDef = Skills.CreateSkillDef<SteppedSkillDef>(
                new SkillDefInfo(
                    "wyatt_primary_combo",
                    WYATT_PREFIX + "PRIMARY_COMBO_NAME",
                    WYATT_PREFIX + "PRIMARY_COMBO_DESCRIPTION",
                    Assets.LoadAsset<Sprite>("texIconWyattPrimary"),
                    new SerializableEntityStateType(typeof(WyattBaseMeleeAttack)),
                    "Weapon",
                    true));
            primarySkillDef.keywordTokens = new string[] {
                 "KEYWORD_AGILE",
                 "KEYWORD_WEIGHTLESS",
                 "KEYWORD_SPIKED",
            };
            primarySkillDef.stepCount = 3;
            primarySkillDef.stepGraceDuration = 0.2f;

            R2API.LanguageAPI.Add(primarySkillDef.skillNameToken, "G22 Grav-Broom");
            R2API.LanguageAPI.Add(primarySkillDef.skillDescriptionToken, 
                "<style=cIsUtility>Agile</style>. Swing twice for <style=cIsDamage>2x90% damage</style>, and finish for <style=cIsDamage>120% damage</style>." +
                "\nWhile on ground, finisher applies <style=cIsUtility>Weightless</style>." +
                "\nwhile in the air, finisher <style=cIsDamage>Spikes</style>");
            R2API.LanguageAPI.Add(primarySkillDef.keywordTokens[1], "<style=cKeywordName>Weightless</style><style=cSub>Slows and removes gravity from target.</style>");
            R2API.LanguageAPI.Add(primarySkillDef.keywordTokens[2], "<style=cKeywordName>Spiking</style><style=cSub>Forces an enemy to travel downwards, causing a shockwave if they impact terrain, dealing <style=cIsDamage>300% damage</style>.</style>");

            Skills.AddPrimarySkills(bodyPrefab, primarySkillDef);
        }

        private void InitializeSecondarySkills()
        {
            GCETSkillDef secondarySkillDef = Skills.CreateSkillDef<GCETSkillDef>(new SkillDefInfo
            {
                skillName = "wyatt_secondary_trashout",
                skillNameToken = WYATT_PREFIX + "SECONDARY_TRASHOUT_NAME",
                skillDescriptionToken = WYATT_PREFIX + "SECONDARY_TRASHOUT_DESCRIPTION",
                skillIcon = Assets.LoadAsset<Sprite>("texIconWyattSecondary"),
                activationState = new SerializableEntityStateType(typeof(TrashOut)),
                activationStateMachineName = "Weapon",
                baseMaxStock = 2,
                baseRechargeInterval = 5f,
                beginSkillCooldownOnSkillEnd = true,
                canceledFromSprinting = false,
                forceSprintDuringState = true,
                fullRestockOnAssign = false,
                interruptPriority = InterruptPriority.Skill,
                resetCooldownTimerOnUse = false,
                isCombatSkill = true,
                mustKeyPress = true,
                cancelSprintingOnActivation = false,
                rechargeStock = 1,
                requiredStock = 1,
                stockToConsume = 1,
                keywordTokens = new string[] { "KEYWORD_SPIKED" }
            });

            R2API.LanguageAPI.Add(secondarySkillDef.skillNameToken, "Trash Out");
            R2API.LanguageAPI.Add(secondarySkillDef.skillDescriptionToken, "<s>Deploy a winch</s> Magically fly towards an enemy, and <style=cIsDamage>Spike</style> them for for <style=cIsDamage>300%</style> damage.");

            Skills.AddSecondarySkills(bodyPrefab, secondarySkillDef);
        }

        private void InitializeUtilitySkills()
        {
            SkillDef utilitySkillDef = Skills.CreateSkillDef(new SkillDefInfo
            {
                skillName = "wyatt_utility_flow",
                skillNameToken = WYATT_PREFIX + "UTILITY_FLOW_NAME",
                skillDescriptionToken = WYATT_PREFIX + "UTILITY_FLOW_DESCRIPTION",
                skillIcon = Assets.LoadAsset<Sprite>("texIconWyattUtility"),
                activationState = new SerializableEntityStateType(typeof(ActivateFlow)),
                activationStateMachineName = "SuperMarioJump",
                baseMaxStock = 1,
                baseRechargeInterval = 14f,
                beginSkillCooldownOnSkillEnd = true,
                canceledFromSprinting = false,
                forceSprintDuringState = false,
                fullRestockOnAssign = false,
                interruptPriority = InterruptPriority.Skill,
                resetCooldownTimerOnUse = false,
                isCombatSkill = true,
                mustKeyPress = false,
                cancelSprintingOnActivation = false,
                rechargeStock = 1,
                requiredStock = 1,
                stockToConsume = 1,
                keywordTokens = new string[] { "KEYWORD_FLOW", }
            });

            R2API.LanguageAPI.Add(utilitySkillDef.skillNameToken, "Flow");
            R2API.LanguageAPI.Add(utilitySkillDef.skillDescriptionToken, "Activate <style=cIsDamage>Flow</style> for 4 seconds + 0.4s for each stack of <style=cIsUtility>Groove</style>, gaining <style=cIsDamage>Armor, a Double Jump, and Cooldown Reduction</style>. During <style=cIsDamage>Flow</style>, you are unable to lose or gain <style=cIsUtility>Groove</style>. After <style=cIsDamage>Flow</style> ends, lose all stacks <style=cIsUtility>Groove</style>.");
            R2API.LanguageAPI.Add("KEYWORD_FLOW", "<style=cKeywordName>Flow</style><style=cSub>Gain <style=cIsDamage>30 Armor + 5</style> for each stack of <style=cIsUtility>Groove</style>.\nGain a <style=cIsUtility>Double Jump</style>.\nGain <style=cIsUtility>+30% Cooldown Reduction.</style></style>");

            Skills.AddUtilitySkills(bodyPrefab, utilitySkillDef);
        }

        private void InitializeSpecialSkills()
        {
            WyattMAIDSkillDef specialSkillDef = Skills.CreateSkillDef<WyattMAIDSkillDef>(new SkillDefInfo
            {
                skillName = "wyatt_special_maid",
                skillNameToken = WYATT_PREFIX + "SPECIAL_MAID_NAME",
                skillDescriptionToken = WYATT_PREFIX + "SPECIAL_MAID_DESCRIPTION",
                skillIcon = Assets.LoadAsset<Sprite>("texIconWyattSpecial"),
                activationState = DeployMaidState,
                activationStateMachineName = "MAID",
                baseMaxStock = 1,
                baseRechargeInterval = 8f,
                beginSkillCooldownOnSkillEnd = true,
                canceledFromSprinting = false,
                forceSprintDuringState = false,
                fullRestockOnAssign = true,
                interruptPriority = InterruptPriority.Skill,
                resetCooldownTimerOnUse = false,
                isCombatSkill = true,
                mustKeyPress = true,
                cancelSprintingOnActivation = true,
                rechargeStock = 1,
                requiredStock = 1,
                stockToConsume = 0,
                keywordTokens = new string[]
                {
                    "KEYWORD_WEIGHTLESS"
                }
            });

            R2API.LanguageAPI.Add(specialSkillDef.skillNameToken, "M88 MAID");
            R2API.LanguageAPI.Add(specialSkillDef.skillDescriptionToken, "<style=cIsUtility>Weightless</style>. Send your <style=cIsUtility>MAID</style> unit barreling through enemies for <style=cIsDamage>300% damage</style> before stopping briefly and returning to you. Activate again to reel toward the <style=cIsUtility>MAID</style> and explode for <style=cIsDamage>500% damage</style>.");

            Skills.AddSpecialSkills(bodyPrefab, specialSkillDef);
        }
        #endregion skills
        public override void InitializeSkins()
        {
            ModelSkinController skinController = prefabCharacterModel.gameObject.AddComponent<ModelSkinController>();
            ChildLocator childLocator = prefabCharacterModel.GetComponent<ChildLocator>();

            CharacterModel.RendererInfo[] defaultRendererinfos = prefabCharacterModel.baseRendererInfos;

            List<SkinDef> skins = new List<SkinDef>();

            #region DefaultSkin
            //this creates a SkinDef with all default fields
            SkinDef defaultSkin = Skins.CreateSkinDef("DEFAULT_SKIN",
                Assets.LoadAsset<Sprite>("texIconWyattSkinDefault"),
                defaultRendererinfos,
                prefabCharacterModel.gameObject);

            //these are your Mesh Replacements. The order here is based on your CustomRendererInfos from earlier
            //pass in meshes as they are named in your assetbundle
            //defaultSkin.meshReplacements = Modules.Skins.getMeshReplacements(defaultRendererinfos,
            //    "meshHenrySword",
            //    "meshHenryGun",
            //    "meshHenry");

            //add new skindef to our list of skindefs. this is what we'll be passing to the SkinController
            skins.Add(defaultSkin);
            #endregion

            //uncomment this when you have a mastery skin
            #region MasterySkin
            /*
            //creating a new skindef as we did before
            SkinDef masterySkin = Modules.Skins.CreateSkinDef(HenryPlugin.DEVELOPER_PREFIX + "_HENRY_BODY_MASTERY_SKIN_NAME",
                Assets.mainAssetBundle.LoadAsset<Sprite>("texMasteryAchievement"),
                defaultRendererinfos,
                prefabCharacterModel.gameObject,
                masterySkinUnlockableDef);

            //adding the mesh replacements as above. 
            //if you don't want to replace the mesh (for example, you only want to replace the material), pass in null so the order is preserved
            masterySkin.meshReplacements = Modules.Skins.getMeshReplacements(defaultRendererinfos,
                "meshHenrySwordAlt",
                null,//no gun mesh replacement. use same gun mesh
                "meshHenryAlt");

            //masterySkin has a new set of RendererInfos (based on default rendererinfos)
            //you can simply access the RendererInfos defaultMaterials and set them to the new materials for your skin.
            masterySkin.rendererInfos[0].defaultMaterial = Modules.Materials.CreateHopooMaterial("matHenryAlt");
            masterySkin.rendererInfos[1].defaultMaterial = Modules.Materials.CreateHopooMaterial("matHenryAlt");
            masterySkin.rendererInfos[2].defaultMaterial = Modules.Materials.CreateHopooMaterial("matHenryAlt");

            //here's a barebones example of using gameobjectactivations that could probably be streamlined or rewritten entirely, truthfully, but it works
            masterySkin.gameObjectActivations = new SkinDef.GameObjectActivation[]
            {
                new SkinDef.GameObjectActivation
                {
                    gameObject = childLocator.FindChildGameObject("GunModel"),
                    shouldActivate = false,
                }
            };
            //simply find an object on your child locator you want to activate/deactivate and set if you want to activate/deacitvate it with this skin

            skins.Add(masterySkin);
            */
            #endregion

            skinController.skins = skins.ToArray();
        }
    }
}