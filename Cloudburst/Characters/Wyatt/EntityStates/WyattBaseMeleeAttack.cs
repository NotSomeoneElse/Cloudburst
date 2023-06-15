﻿using System;
using Cloudburst.Characters;
using Cloudburst.Characters.Wyatt;
using Cloudburst.Wyatt.Components;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace Cloudburst.CEntityStates.Wyatt
{
    //TODO:
    //Fix the combo finisher being weird.

    class WyattBaseMeleeAttack : BasicMeleeAttack, SteppedSkillDef.IStepSetter
    {

        public int step = 0;
        public static float recoilAmplitude = 0.7f;
        public static float percentDurationBeforeInterruptable = 0.6f;
        public float bloom = 1f;
        //public static float comboFinisherBaseDuration = 0.5f;
        //public static float comboFinisherhitPauseDuration = 0.15f;
        //public static float comboFinisherBloom = 0.5f;
        //public static float comboFinisherBaseDurationBeforeInterruptable = 0.5f;
        //private string animationStateName;
        private float durationBeforeInterruptable;

        private bool isComboFinisher
        {
            get
            {
                return this.step == 2;
            }
        }
        
        private bool spawnEffect = false;
        private string animationStateName;
        private bool isUppercut;

        public override bool allowExitFire
        {
            get
            {
                return base.characterBody && !base.characterBody.isSprinting;
            }
        }

        public override void OnEnter()
        {
            this.hitBoxGroupName = "TempHitboxLarge";
            if (isComboFinisher) this.hitBoxGroupName = "TempHitbox";
            this.mecanimHitboxActiveParameter = GetMecanimActiveParameter();

            this.baseDuration = WyattConfig.M1AttackDuration.Value;// 0.5f;
            if (isComboFinisher) baseDuration = WyattConfig.M1AttackDurationFinisher.Value;// 0.8f;
            this.duration = this.baseDuration / base.attackSpeedStat;

            this.hitPauseDuration = 0.02f;
            if (isComboFinisher) hitPauseDuration = 0.1f;

            this.damageCoefficient = WyattConfig.M1Damage.Value; //1;
            if (isComboFinisher) damageCoefficient = WyattConfig.M1DamageFinisher.Value;// 2f;
            //this.damageCoefficient = (2f + (characterBody.GetBuffCount(Custodian.instance.wyattCombatDef) * 0.1f));
            this.procCoefficient = 1f;
            this.durationBeforeInterruptable = percentDurationBeforeInterruptable * duration;
            this.shorthopVelocityFromHit = 3;
            if (isComboFinisher) shorthopVelocityFromHit = 5f;
            isUppercut = base.isGrounded;

            spawnEffect = false;
            //swingEffectPrefab = BandaidConvert.Resources.Load<GameObject>("prefabs/effects/GrandparentGroundSwipeTrailEffect");
            hitEffectPrefab = LegacyResourcesAPI.Load<GameObject>("prefabs/effects/omnieffect/omniimpactvfxmedium");
            //swingEffectMuzzleString = "WinchHole";//"//SwingTrail";

            /*EffectManager.SpawnEffect(Effects.shaderEffect, new EffectData()
            {
                origin = base.transform.position,
            }, false);*/

            if (isComboFinisher)
            {
                this.hitBoxGroupName = "TempHitbox";
                //if (isUppercut)
                //{
                //    forceVector = new Vector3(0, 1000, 0);
                //}
            }

            base.OnEnter();
        }

        private string GetMecanimActiveParameter()
        {
            switch (step)
            {
                default:
                case 0:
                    return "BroomSwing1.active";
                case 1:
                    return "BroomSwing2.active";
                case 2:
                    return "BroomSwing3.active";
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            StartAimMode();
        }


        public override void BeginMeleeAttackEffect()
        {
            if (!spawnEffect)
            {
                spawnEffect = true;
                if (base.isAuthority)
                {

                   // EffectManager.SimpleMuzzleFlash(obj, base.gameObject, "SwingTrail", true);
                }
            }
        }


        public override void OnExit()
        {
            base.OnExit();

        }

        public override void AuthorityModifyOverlapAttack(OverlapAttack overlapAttack)
        {
            base.AuthorityModifyOverlapAttack(overlapAttack);
            //despite what the animation is playing, decided I want to decide when it lands what the hit does
            if (this.isComboFinisher && base.isGrounded)
            {
                //overlapAttack.damageType = DamageTypeCore.antiGrav | DamageType.Generic;
                R2API.DamageAPI.AddModdedDamageType(overlapAttack, WyattDamageTypes.antiGravDamage); 
            } else
            {
                R2API.DamageAPI.RemoveModdedDamageType(overlapAttack, WyattDamageTypes.antiGravDamage);
            }
        }        

        public override void PlayAnimation()
        {
            /*EffectManager.SpawnEffect(Effects.blackHoleIncisionEffect, new EffectData()
            {
                origin = base.transform.position,
                scale = 10,
                rotation = Quaternion.identity, 
            }, false);*/
            this.animationStateName = "";
            switch (this.step)
            {
                case 0:
                    this.animationStateName = "Swing1";
                    break;
                case 1:
                    this.animationStateName = "Swing2";
                    break;
                case 2:
                    if (isUppercut)
                    {
                        this.animationStateName = "Swing3";
                    } else
                    {
                        animationStateName = "Swing3-2";
                    }
                    break;
            }
            //bool moving = this.animator.GetBool("isMoving");
            //bool grounded = this.animator.GetBool("isGrounded");

            //if (!moving && grounded)
            //{
            //    base.PlayCrossfade("FullBody, Override", this.animationStateName, "BroomSwing.playbackRate", this.duration, 0.05f);
            //}

            base.PlayCrossfade("Gesture, Override", this.animationStateName, "BroomSwing.playbackRate", this.duration, 0.05f);
        }

        public override void OnMeleeHitAuthority()
        {
            base.OnMeleeHitAuthority();
            base.characterBody.AddSpreadBloom(this.bloom);
            if (isComboFinisher)
            {
                WyattNetworkCombat networkCombat = GetComponent<WyattNetworkCombat>();
                for (int i = 0; i < hitResults.Count; i++)
                {
                    HurtBox hurtBox = hitResults[i];
                    if(hurtBox.healthComponent.gameObject == null)
                        continue;

                    //despite what the animation is playing, decided I want to decide when it lands what the hit does
                    if (/*isUppercut*/base.isGrounded)
                    {
                        networkCombat.ApplyKnockupAuthority(hurtBox.healthComponent.body.gameObject, WyattConfig.M1UpwardsLiftForce.Value);
                    }
                    else
                    {
                        CharacterMotor motor = hurtBox.healthComponent.body.characterMotor;
                        if (!motor || (motor && !motor.isGrounded))
                        {
                            networkCombat.ApplyBasedAuthority(hurtBox.healthComponent.body.gameObject, gameObject, 1);
                        }
                    }
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            if (base.fixedAge >= this.durationBeforeInterruptable)
            {
                return InterruptPriority.Any;
            }
            return InterruptPriority.Skill;
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write((byte)this.step);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            this.step = (int)reader.ReadByte();
        }

        void SteppedSkillDef.IStepSetter.SetStep(int i)
        {
            this.step = i;
        }
    }
}
