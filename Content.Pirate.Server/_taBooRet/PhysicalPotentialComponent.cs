using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Server._taBooRet
{
    /// <summary> 
    /// Represents a single unit of training progress to be processed. 
    /// </summary> 
    [DataDefinition]
    public sealed partial class TrainingStrain
    {
        [DataField("power")]
        public DamageSpecifier Damage { get; set; } = new();
    }

    /// <summary> 
    /// Tracks and processes physical training progress for an entity. 
    /// </summary> 
    [RegisterComponent, NetworkedComponent]
    public sealed partial class PhysicalPotentialComponent : Component
    {
        [DataField("strains")]
        public List<TrainingStrain> Strains = new();

        [DataField("damageBonus")]
        public DamageSpecifier DamageBonus = new();

        [DataField("damageRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public float DamageRisingSpeed = 1f;

        [DataField("timeForRest"), ViewVariables(VVAccess.ReadWrite)]
        public float TimeForRest = 60f;

        [DataField("hungerCost"), ViewVariables(VVAccess.ReadWrite)]
        public float HungerCost = 1f;

        [DataField("strainsApplyingDelay"), ViewVariables(VVAccess.ReadWrite)]
        public float StrainsApplyingDelay = 0.5f;

        // -- State Tracking (No DataFields needed for these) -- 
        [ViewVariables] public TimeSpan EndRestTime;
        [ViewVariables] public bool IsResting;
        [ViewVariables] public TimeSpan NextStrainTime;
    }
}
