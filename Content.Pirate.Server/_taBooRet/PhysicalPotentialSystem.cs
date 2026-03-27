using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Sprinting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;
using Content.Shared._taBooRet;
using Robust.Shared.Random;

namespace Content.Server._taBooRet
{
    public sealed class PhysicalPotentialSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly HungerSystem _hungerSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PhysicalPotentialComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<PhysicalPotentialComponent, DamageModifyEvent>(OnDamageModify);
            SubscribeLocalEvent<PhysicalPotentialComponent, StoodEvent>(OnStood);

           
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var query = EntityQueryEnumerator<PhysicalPotentialComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateSprintProgress(frameTime, uid, comp);
                HandleRecovery(uid, comp);
            }
        }

        #region Calculate strains
        // -- HITS -- 
        private void OnMeleeHit(EntityUid uid, PhysicalPotentialComponent comp, MeleeHitEvent args)
        {
            if (!TryComp<MeleeWeaponComponent>(uid, out var melee)) return;

            foreach (var hitEntity in args.HitEntities)
            {
                if (!TryComp<MobStateComponent>(hitEntity, out var mob)) continue;
                if (mob.CurrentState != MobState.Alive) continue;

                var damageStrain = GetDamageStain(uid, comp, melee);
                // Create and queue a new training strain
                var newStrain = new TrainingStrain { Damage = damageStrain };
                AddStrain(comp, newStrain);
            }

            // Apply current bonus to the final attack damage 
            args.BonusDamage += comp.DamageBonus;
        }

        public DamageSpecifier GetDamageStain(EntityUid uid, PhysicalPotentialComponent comp, MeleeWeaponComponent melee)
        {
            // Extract raw damage values for Blunt and Slash types from the weapon's damage dictionary
            var blunt = (float) melee.Damage.DamageDict.GetValueOrDefault("Blunt", 0);
            var slash = (float) melee.Damage.DamageDict.GetValueOrDefault("Slash", 0);
            var totalDamage = blunt + slash;

            var damageStrain = new DamageSpecifier();

            if (totalDamage > 0)
            {
                // Calculate the ratio of each damage type relative to the total damage
                // This ensures the strain is proportional to the weapon's damage profile
                damageStrain.DamageDict["Blunt"] = FixedPoint2.New(blunt / totalDamage);
                damageStrain.DamageDict["Slash"] = FixedPoint2.New(slash / totalDamage);
            }
            else
            {
                damageStrain.DamageDict["Blunt"] = FixedPoint2.New(0.01);
            }

            damageStrain *= comp.DamageRisingSpeed;
            return damageStrain;
        }

        // -- DAMAGE --
        private void OnDamageModify(EntityUid uid, PhysicalPotentialComponent comp, DamageModifyEvent args)
        {
            if (args.Origin == null) return;

            var newStrain = new TrainingStrain { Defense = comp.DefenseRisingSpeed };
            AddStrain(comp, newStrain);

            //Reduces incoming damage
            if (args.Damage.DamageDict.ContainsKey("Blunt"))
            {
                args.Damage.DamageDict["Blunt"] -= comp.DefenseBonus;
            }

            if (args.Damage.DamageDict.ContainsKey("Slash"))
            {
                args.Damage.DamageDict["Slash"] -= comp.DefenseBonus;
            }
        }

        // -- PUSH-UP --
        private void OnStood(EntityUid uid, PhysicalPotentialComponent comp, StoodEvent args)
        {
            if (!TryComp<MeleeWeaponComponent>(uid, out var melee)) return;

            var damageStrain = GetDamageStain(uid, comp, melee);

            // Create and queue a new training strain 
            var newStrain = new TrainingStrain
            {
                Damage = damageStrain * comp.PushUpsEfficiency,
                Defense = comp.DefenseRisingSpeed * comp.PushUpsEfficiency,
                Stamina = comp.StaminaRisingSpeed * comp.PushUpsEfficiency
            };

            AddStrain(comp, newStrain);
        }

        // -- STAMINA AND SPRINT --
        private void UpdateSprintProgress(float frameTime, EntityUid uid, PhysicalPotentialComponent comp)
        {
            if (!TryComp<SprinterComponent>(uid, out var sprinter)) return;

            if (sprinter.IsSprinting)
            {
                comp.SprintTimer += frameTime;

                // Check if the sprint duration has exceeded the defined interval for a "tick"
                if (comp.SprintTimer > comp.SprintInterval)
                {
                    comp.SprintTimer = 0;

                    var newStrain = new TrainingStrain { Stamina = comp.StaminaRisingSpeed };
                    AddStrain(comp, newStrain);
                }
            }
        }

        #endregion

        #region Strain Handling 
        // Adds a new training point to the processing queue 
        public void AddStrain(PhysicalPotentialComponent comp, TrainingStrain strain)
        {
            if (comp.Strains.Count < comp.MaxStrainsNumber)
            {
                int fullExecutions = (int) MathF.Floor(comp.trainingEffectiveness);

                for (int i = 0; i < fullExecutions; i++)
                {
                    comp.Strains.Add(strain);
                }

                float remainder = comp.trainingEffectiveness - fullExecutions;
                if (_random.Prob(remainder))
                {
                    comp.Strains.Add(strain);
                }
            }

            // Set cooldown (rest period) before training absorption begins 
            comp.EndRestTime = _timing.CurTime + TimeSpan.FromSeconds(comp.TimeForRest);
            comp.IsResting = true;
        }

        private void HandleRecovery(EntityUid uid, PhysicalPotentialComponent comp)
        {
            if (!TryComp<MobStateComponent>(uid, out var mob) || mob.CurrentState != MobState.Alive) return;

            // Check if the rest period after the last activity has ended 
            if (comp.IsResting && comp.EndRestTime < _timing.CurTime)
            {
                comp.IsResting = false;
            }

            // Gradually process the strain queue if the player is resting 
            if (!comp.IsResting && comp.Strains.Count > 0)
            {
                // Introduce a delay between iterations for smooth bonus progression 
                if (comp.NextStrainTime < _timing.CurTime)
                {
                    ApplyStrain(uid, comp);
                    comp.NextStrainTime = _timing.CurTime + TimeSpan.FromSeconds(comp.StrainsApplyingDelay);
                }
            }
        }

        // Apply a specific strain point to the character's stats 
        private void ApplyStrain(EntityUid uid, PhysicalPotentialComponent comp)
        {
            if (comp.Strains.Count == 0) return;

            var strain = comp.Strains[comp.Strains.Count - 1];

            // Update damage bonus
            if (comp.DamageBonus.GetTotal() < comp.MaxDamageBonus)
            {
                comp.DamageBonus += strain.Damage;
            }

            // Update defense bonus
            if (comp.DefenseBonus < comp.MaxDefenseBonus)
            {
                comp.DefenseBonus += strain.Defense;
            }

            // Update stamina bonus
            if (TryComp<StaminaComponent>(uid, out var stamina))
            {
                if (stamina.CritThreshold < comp.MaxStamina)
                stamina.CritThreshold += strain.Stamina;
            }

            comp.Strains.RemoveAt(comp.Strains.Count -1);

            // Mark component as dirty to sync data with the client 
            Dirty(uid, comp);

            // Deduct hunger/calories for training 
            if (TryComp<HungerComponent>(uid, out var hunger))
            {
                _hungerSystem.ModifyHunger(uid, -comp.HungerCost, hunger);
            }
        }
        #endregion
    }
}
