using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server._taBooRet
{
    public sealed class PhysicalPotentialSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly HungerSystem _hungerSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PhysicalPotentialComponent, MeleeHitEvent>(OnMeleeHit);
        }

        // -- CALCULATE STRAIN FROM MELEE HITS -- 
        private void OnMeleeHit(EntityUid uid, PhysicalPotentialComponent comp, MeleeHitEvent args)
        {
            if (!TryComp<MeleeWeaponComponent>(uid, out var melee)) return;

            foreach (var hitEntity in args.HitEntities)
            {
                if (!TryComp<MobStateComponent>(hitEntity, out var mob)) continue;
                if (mob.CurrentState != MobState.Alive) continue;

                // Determine base damage values (Blunt/Slash) 
                var blunt = melee.Damage.DamageDict.GetValueOrDefault("Blunt", 0);
                var slash = melee.Damage.DamageDict.GetValueOrDefault("Slash", 0);
                var totalDamage = blunt + slash;

                // Prevent division by zero 
                if (totalDamage <= 0) return;

                // Distribute strain proportionally based on damage types 
                var damageStrain = new DamageSpecifier();
                damageStrain.DamageDict["Blunt"] = blunt / totalDamage;
                damageStrain.DamageDict["Slash"] = slash / totalDamage;

                // Create and queue a new training strain 
                var newStrain = new TrainingStrain { Damage = damageStrain * comp.DamageRisingSpeed };
                AddStrain(args.User, comp, newStrain);
            }

            // Apply current bonus to the final attack damage 
            args.BonusDamage += comp.DamageBonus;
        }

        // -- ACCUMULATED STRAIN PROCESSING -- 
        #region Strain Handling 
        // Adds a new training point to the processing queue 
        private void AddStrain(EntityUid user, PhysicalPotentialComponent comp, TrainingStrain strain)
        {
            comp.Strains.Add(strain);
            // Set cooldown (rest period) before training absorption begins 
            comp.EndRestTime = _timing.CurTime + TimeSpan.FromSeconds(comp.TimeForRest);
            comp.IsResting = true;
            Logger.Info($"{user} received strain: {strain.Damage.GetTotal()}");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var query = EntityQueryEnumerator<PhysicalPotentialComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
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
        }

        // Apply a specific strain point to the character's stats 
        private void ApplyStrain(EntityUid user, PhysicalPotentialComponent comp)
        {
            if (comp.Strains.Count == 0) return;

            // Fetch the oldest strain from the queue (FIFO) 
            var strain = comp.Strains[0];

            // Update damage bonus 
            comp.DamageBonus += strain.Damage;
            comp.Strains.RemoveAt(0);

            // Mark component as dirty to sync data with the client 
            Dirty(user, comp);

            // Deduct hunger/calories for training 
            if (TryComp<HungerComponent>(user, out var hunger))
            {
                _hungerSystem.ModifyHunger(user, -comp.HungerCost, hunger);
            }
            Logger.Info($"{user} grew stronger. Current bonus: {comp.DamageBonus.GetTotal()}");
        }
        #endregion
    }
}
