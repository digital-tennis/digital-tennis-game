using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Small placeholder swing animation, independent of ball and score rules.</summary>
    public sealed class RacketAvatar : MonoBehaviour
    {
        [SerializeField] private Transform racketPivot;
        public const float SwingDuration = .35f;
        private float swingRemaining;
        private bool hasRemoteSwing;
        public uint SwingId { get; private set; }
        public StrokeKind LastStroke => stroke;
        public float SwingAge => SwingDuration - swingRemaining;
        public bool IsSwinging => swingRemaining > 0;
        public int RemoteForehands { get; private set; }
        public int RemoteBackhands { get; private set; }
        private StrokeKind stroke;
        public float DistanceTravelled { get; private set; }
        public Vector3 Target { get; private set; }

        public void MoveTo(Vector3 target, CourtSide player, float deltaTime, float speed = AutomaticCourtMovement.Speed)
        {
            Target = target;
            Vector3 before = transform.position;
            transform.position = AutomaticCourtMovement.Step(before, target, player, deltaTime, speed);
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

        public void Swing(StrokeKind kind)
        {
            stroke = kind; swingRemaining = SwingDuration;
            if (++SwingId == 0) SwingId = 1;
        }

        public void PresentRemoteSwing(uint id, StrokeKind kind, float age)
        {
            if (id == 0 || !System.Enum.IsDefined(typeof(StrokeKind), kind) ||
                !BallSimulation.IsFinite(age) || age < 0 ||
                (hasRemoteSwing && id != SwingId && !Sequence32.IsNewer(id, SwingId))) return;
            if (!hasRemoteSwing || id != SwingId)
            {
                hasRemoteSwing = true; SwingId = id; stroke = kind;
                if (age < SwingDuration)
                {
                    if (kind == StrokeKind.Forehand) RemoteForehands++; else if (kind == StrokeKind.Backhand) RemoteBackhands++;
                }
            }
            else if (kind != stroke) return;
            // Old snapshots and a reconnect checkpoint do not replay an already completed swing.
            swingRemaining = Mathf.Max(0, SwingDuration - age);
        }

        private void Update()
        {
            if (racketPivot == null) return;
            swingRemaining = Mathf.Max(0f, swingRemaining - Time.deltaTime);
            float arc = swingRemaining > 0f ? Mathf.Sin((1f - swingRemaining / SwingDuration) * Mathf.PI) * 95f : 0f;
            float side = stroke == StrokeKind.Backhand ? -1f : 1f;
            racketPivot.localPosition = new Vector3(side * 0.8f, 0.3f, 0.3f);
            racketPivot.localRotation = Quaternion.Euler(0f, side * arc, -side * arc * 0.25f);
            if (stroke == StrokeKind.Smash) racketPivot.localRotation = Quaternion.Euler(-arc, 0, 0);
            else if (stroke == StrokeKind.Volley) racketPivot.localRotation = Quaternion.Euler(0, side * arc * .35f, 10);
            else if (stroke == StrokeKind.Lob) racketPivot.localRotation = Quaternion.Euler(-arc * .5f, side * arc * .4f, 0);
            else if (stroke == StrokeKind.Slice) racketPivot.localRotation = Quaternion.Euler(45, side * arc * .5f, 30);
        }
    }
}
