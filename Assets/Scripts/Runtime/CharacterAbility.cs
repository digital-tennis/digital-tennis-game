using UnityEngine;

namespace DigitalTennis
{
    public sealed class SkillMeter
    {
        public const int Maximum = 3;
        public int Stock { get; private set; }
        public float Gauge { get; private set; }
        public void Gain(float amount)
        {
            if (!BallSimulation.IsFinite(amount) || amount <= 0 || Stock == Maximum) return;
            Gauge += amount;
            while (Gauge >= 100 && Stock < Maximum) { Stock++; Gauge -= 100; }
            if (Stock == Maximum) Gauge = 100; // Full is a display sentinel, never a hidden fourth stock.
        }
        public bool Spend()
        {
            if (Stock == 0) return false;
            if (Stock == Maximum) Gauge = 0;
            Stock--; return true;
        }
        public void Present(int stock, float gauge) { Stock = stock; Gauge = gauge; }
    }

    [System.Serializable] public sealed class CharacterState
    {
        public int index, stock;
        public float gauge, energy = 100, remaining, cinematicAge = 1.2f;
        public uint activationId;
        public CharacterBalance balance;
        public bool IsValid => CharacterCatalog.ValidIndex(index) && balance != null && balance.id == CharacterCatalog.Ids[index] && balance.IsValid &&
            stock >= 0 && stock <= 3 && BallSimulation.IsFinite(gauge) && gauge >= 0 && (stock == 3 ? gauge == 100 : gauge < 100) &&
            BallSimulation.IsFinite(energy) && energy >= 0 && energy <= 100 && BallSimulation.IsFinite(remaining) && remaining >= 0 && remaining <= 10 &&
            BallSimulation.IsFinite(cinematicAge) && cinematicAge >= 0 && cinematicAge <= CharacterAbility.CinematicSeconds;
    }

    public sealed class CharacterAbility
    {
        public const float CinematicSeconds = 1.2f;
        public int Index { get; private set; }
        public CharacterBalance Balance { get; private set; }
        public SkillMeter Meter { get; } = new SkillMeter();
        public float Energy { get; private set; } = 100;
        public float Remaining { get; private set; }
        public float CinematicAge { get; private set; } = CinematicSeconds;
        public uint ActivationId { get; private set; }
        public bool Cinematic => CinematicAge < CinematicSeconds;
        public bool Buff => Remaining > 0 && !Cinematic;
        private GameplayBalance tuning;
        private float V(string key) => tuning[key];
        private float Rating(float r) => 1 + (r - V("rating.center")) * V("rating.scale");
        public float MoveSpeed => V("move.speed") * Rating(Balance.speed) * Mathf.Lerp(V("energy.move_floor"), 1, Energy / 100) * (Buff ? Balance.speedMultiplier : 1);
        public float Reach => V("hit.reach") * Rating(Balance.defense) * (Buff ? Balance.defenseMultiplier : 1);
        public float ServeSpeed => Rating(Balance.serve) * (Buff ? Balance.serveMultiplier : 1);
        public CharacterAbility(int index, CharacterBalance balance, GameplayBalance tuning = null) { Index = index; Balance = balance; this.tuning = tuning ?? GameplayBalance.Defaults; }
        public void Rebalance(CharacterBalance balance, GameplayBalance settings = null) { Balance = balance; if(settings!=null)tuning=settings; }
        public float ShotMultiplier(StrokeKind stroke)
        {
            float rating = stroke == StrokeKind.Smash ? Rating(Balance.smash) : stroke == StrokeKind.Volley ? Rating(Balance.volley) : 1;
            float buff = !Buff ? 1 : stroke == StrokeKind.Smash ? Balance.smashMultiplier : stroke == StrokeKind.Volley ? Balance.volleyMultiplier : Balance.shotMultiplier;
            return rating * buff * Mathf.Lerp(V("energy.shot_floor"), 1, Energy / 100);
        }
        public void Returned(float incomingSpeed)
        {
            Meter.Gain(Balance.attackGain + Balance.defenseGain * Rating(Balance.defense) + (incomingSpeed > V("meter.fast_ball") ? V("meter.fast_bonus") : 0));
            Energy = Mathf.Max(0, Energy - V("energy.hit_cost") * (V("energy.hit_factor") - Balance.stamina * V("energy.hit_stamina")));
        }
        public void ValidServe() => Meter.Gain(Balance.attackGain * V("meter.serve_gain"));
        public void Moved(float distance) => Energy = Mathf.Max(0, Energy - distance * (V("energy.move_cost") - Balance.stamina * V("energy.move_stamina")));
        public bool Activate()
        {
            if (Remaining > 0 || Cinematic || !Meter.Spend()) return false;
            ActivationId++; CinematicAge = 0; Remaining = Balance.duration;
            Energy = Mathf.Min(100, Energy + Balance.restore); return true;
        }
        public void Tick(float dt, bool courtIdle)
        {
            if (!BallSimulation.IsFinite(dt) || dt <= 0) return;
            if (Cinematic) CinematicAge = Mathf.Min(CinematicSeconds, CinematicAge + dt);
            else Remaining = Mathf.Max(0, Remaining - dt);
            if (courtIdle && !Cinematic) Energy = Mathf.Min(100, Energy + dt * (V("energy.recovery") + Balance.stamina * V("energy.recovery_stamina")));
        }
        public CharacterState Capture() => new CharacterState { index = Index, balance = Balance, stock = Meter.Stock, gauge = Meter.Gauge,
            energy = Energy, remaining = Remaining, activationId = ActivationId, cinematicAge = CinematicAge };
        public void Present(CharacterState state)
        {
            Index = state.index; Balance = state.balance; Meter.Present(state.stock, state.gauge); Energy = state.energy;
            Remaining = state.remaining; ActivationId = state.activationId; CinematicAge = state.cinematicAge;
        }
    }
}
