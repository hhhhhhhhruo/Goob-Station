using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._taBooRet
{
    /// <summary> 
    /// Represents a single unit of training progress to be processed. 
    /// </summary> 
    [DataDefinition]
    public sealed partial class TrainingStrain
    {
        [DataField("power")]
        public DamageSpecifier Damage { get; set; } = new();

        public FixedPoint2 Defense { get; set; } = new FixedPoint2();

        public float Stamina { get; set; }
    }

    /// <summary> 
    /// Tracks and processes physical training progress for an entity. 
    /// </summary> 
    [RegisterComponent, NetworkedComponent]
    public sealed partial class PhysicalPotentialComponent : Component
    {
        #region Damage
        [DataField("strains")]
        public List<TrainingStrain> Strains = new();

        [DataField("damageBonus")]
        public DamageSpecifier DamageBonus = new();

        [DataField("damageRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 DamageRisingSpeed = 0.02f;
        #endregion

        #region Defense
        [DataField("defenseRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 DefenseRisingSpeed = 0.02f;

        [DataField("defenseBonus")]
        public FixedPoint2 DefenseBonus = new();
        #endregion

        #region Stamina and Sprint
        [DataField("staminaRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public float StaminaRisingSpeed = 0.2f;

        public bool IsSprinting;

        public float SprintTimer;

        [DataField("sprintInterval"), ViewVariables(VVAccess.ReadWrite)]
        public float SprintInterval = 1;
        #endregion

        [DataField("pushUpsEfficiency"), ViewVariables(VVAccess.ReadWrite)]
        public float PushUpsEfficiency = 0.3f;

        #region Rest
        [DataField("timeForRest"), ViewVariables(VVAccess.ReadWrite)]
        public float TimeForRest = 180f;

        [ViewVariables] public TimeSpan EndRestTime;
        [ViewVariables] public bool IsResting;
        [ViewVariables] public TimeSpan NextStrainTime;
        #endregion

        #region Strain
        [DataField("strainsApplyingDelay"), ViewVariables(VVAccess.ReadWrite)]
        public float StrainsApplyingDelay = 0.5f;

        [DataField("hungerCost"), ViewVariables(VVAccess.ReadWrite)]
        public float HungerCost = 1f;
        #endregion
    }
}
