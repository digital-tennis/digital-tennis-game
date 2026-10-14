using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Small placeholder swing animation, independent of ball and score rules.</summary>
    public sealed class RacketAvatar : MonoBehaviour
    {
        [SerializeField] private Transform racketPivot;
        private float swingRemaining;
        private StrokeKind stroke;
        public float DistanceTravelled { get; private set; }
        public Vector3 Target { get; private set; }

        public void MoveTo(Vector3 target, CourtSide player, float deltaTime)
        {
            Target = target;
            Vector3 before = transform.position;
            transform.position = AutomaticCourtMovement.Step(before, target, player, deltaTime);
            DistanceTravelled += Vector3.Distance(before, transform.position);
        }

        public void ResetPose(Vector3 position)
        {
            transform.position = position;
            Target = position;
            DistanceTravelled = 0f;
            swingRemaining = 0f;
            if (racketPivot != null) racketPivot.localRotation = Quaternion.identity;
        }

        public void Swing(StrokeKind kind) { stroke = kind; swingRemaining = 0.35f; }

        private void Update()
        {
            if (racketPivot == null) return;
            swingRemaining = Mathf.Max(0f, swingRemaining - Time.deltaTime);
            float arc = swingRemaining > 0f ? Mathf.Sin((1f - swingRemaining / 0.35f) * Mathf.PI) * 95f : 0f;
            float side = stroke == StrokeKind.Backhand ? -1f : 1f;
            racketPivot.localPosition = new Vector3(side * 0.8f, 0.3f, 0.3f);
            racketPivot.localRotation = Quaternion.Euler(0f, side * arc, -side * arc * 0.25f);
        }
    }
}
