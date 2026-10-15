using System;
using UnityEngine;

namespace DigitalTennis
{
    [DisallowMultipleComponent, RequireComponent(typeof(TennisMatchController))]
    public sealed class PlayerInputAdapter : MonoBehaviour
    {
        private readonly PlayerInputRouter router = new PlayerInputRouter();
        private TennisMatchController controller;
        private uint buttonSequence;
        public float Power01 { get; set; } = .35f;
        public float AimX { get; set; }
        public float AimZ { get; set; }
        public bool InputEnabled { get; set; } = true;
        public bool Typing { get; set; }
        public string SourceName => router.Source?.Name ?? "None";
        public Action<PlayerInputSample, uint> OnlineRoute { get; set; }
        public int Delivered { get; private set; }
        public int Rejected => router.Rejected;

        private void Awake()
        {
            controller = GetComponent<TennisMatchController>();
            UseSource(new KeyboardPlayerInputSource());
        }

        public void UseSource(IPlayerInputSource source) => router.SetSource(source);
        public void UseKeyboard() => UseSource(new KeyboardPlayerInputSource());

        private void Update()
        {
            if (!InputEnabled || Typing) { router.Source?.Clear(); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            router.Source?.Poll(now, Power01, AimX, AimZ);
            router.Drain(now, Deliver);
        }

        /// <summary>On-screen buttons use the same command route as keyboard and future hardware.</summary>
        public void Press(PlayerAction action, bool practiceOpponent = false)
        {
            if (!InputEnabled) return;
            if (++buttonSequence == 0) buttonSequence = 1;
            Deliver(new PlayerInputSample(action, buttonSequence, Time.realtimeSinceStartupAsDouble,
                Power01, AimX, AimZ, practiceOpponent), 999);
        }

        private void Deliver(PlayerInputSample sample, uint session)
        {
            if (!sample.IsValid) return;
            Delivered++;
            if (OnlineRoute != null) OnlineRoute(sample, session);
            else controller.ApplyPlayerInput(sample, session, CourtSide.Near);
        }
    }
}
