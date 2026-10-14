using System;
using UnityEngine;

namespace DigitalTennis
{
    [DisallowMultipleComponent]
    public sealed class BallFlight : MonoBehaviour
    {
        [SerializeField] private BallPhysicsSettings settings = new BallPhysicsSettings();
        [SerializeField] private bool isAuthoritative = true;
        private BallSimulation simulation;
        private TrailRenderer trail;

        public event Action<BallEvent> EventRaised;
        public BallSimulation Simulation => simulation;
        public bool IsAuthoritative { get => isAuthoritative; set => isAuthoritative = value; }

        private void Awake()
        {
            simulation = new BallSimulation(settings);
            trail = GetComponent<TrailRenderer>();
        }

        public void Launch(Vector3 position, Vector3 velocity, ShotContext shot)
        {
            if (!isAuthoritative) return;
            simulation.Launch(position, velocity, shot);
            transform.position = position;
            if (trail != null) trail.Clear();
        }

        private void FixedUpdate()
        {
            if (!isAuthoritative || !simulation.HasShot) return;
            var events = simulation.Step();
            transform.position = simulation.Position;
            // Publish only after the complete tick. A listener may start the next shot.
            if (events.Count == 0) return;
            var published = new BallEvent[events.Count];
            for (int i = 0; i < events.Count; i++) published[i] = events[i];
            foreach (var item in published) EventRaised?.Invoke(item);
        }
    }
}
