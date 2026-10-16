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
        public bool LastSwingContact {get;private set;}
        public bool IsServe {get;private set;}
        public bool HasContactPoint {get;private set;}
        public Vector3 ContactPoint {get;private set;}
        public Transform RacketPivot=>racketPivot;
        public Vector3 StringCenter=>racketPivot.TransformPoint(new Vector3(0,.8f,0));
        public int Misses {get;private set;}
        public int RemoteMisses {get;private set;}
        public int RemoteForehands { get; private set; }
        public int RemoteBackhands { get; private set; }
        private StrokeKind stroke;
        private AthleteMotion athlete;
        private TennisMatchController controller;
        private void Start(){controller=Object.FindFirstObjectByType<TennisMatchController>();}
        public float DistanceTravelled { get; private set; }
        public Vector3 Target { get; private set; }

        public void MoveTo(Vector3 target, CourtSide player, float deltaTime, float speed = float.NaN, GameplayBalance settings = null)
        {
            Target = target;
            Vector3 before = transform.position;
            transform.position = AutomaticCourtMovement.Step(before, target, player, deltaTime, speed,settings);
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

        public void Swing(StrokeKind kind,bool contact=true,bool serving=false,Vector3? contactPoint=null)
        {
            HasContactPoint=contact&&contactPoint.HasValue;ContactPoint=contactPoint??Vector3.zero;
            stroke = kind;IsServe=serving;LastSwingContact=contact;if(!contact)Misses++; swingRemaining = SwingDuration;
            if (++SwingId == 0) SwingId = 1;
            if(HasContactPoint)GetComponentInChildren<CharacterPresentation>()?.SynchronizeVisualPose();
        }

        public void PresentRemoteSwing(uint id, StrokeKind kind, float age,bool contact=true,bool serving=false,Vector3? contactPoint=null)
        {
            if (id == 0 || !System.Enum.IsDefined(typeof(StrokeKind), kind) ||
                !BallSimulation.IsFinite(age) || age < 0 ||
                (hasRemoteSwing && id != SwingId && !Sequence32.IsNewer(id, SwingId))) return;
            if (!hasRemoteSwing || id != SwingId)
            {
                hasRemoteSwing = true; HasContactPoint=contact&&contactPoint.HasValue;ContactPoint=contactPoint??Vector3.zero; SwingId = id; stroke = kind;IsServe=serving;LastSwingContact=contact;
                if (age < SwingDuration)
                {
                    if(!contact)RemoteMisses++;
                    if (kind == StrokeKind.Forehand) RemoteForehands++; else if (kind == StrokeKind.Backhand) RemoteBackhands++;
                }
            }
            else if (kind != stroke||contact!=LastSwingContact||serving!=IsServe||HasContactPoint!=(contact&&contactPoint.HasValue)) return;
            if(HasContactPoint&&contactPoint.HasValue&&Vector3.Distance(ContactPoint,contactPoint.Value)>.001f)return;
            // Old snapshots and a reconnect checkpoint do not replay an already completed swing.
            swingRemaining = Mathf.Max(0, SwingDuration - age);
        }

        private void Update()
        {
            if (racketPivot == null) return;
            if(controller==null||(!controller.IsPaused&&!(controller.Characters?.Cinematic??false)))swingRemaining = Mathf.Max(0f, swingRemaining - Time.deltaTime);
            if(athlete==null)athlete=GetComponentInChildren<AthleteMotion>();
            if(athlete!=null){athlete.LockGrip();return;}
            float arc = swingRemaining > 0f ? Mathf.Sin((1f - swingRemaining / SwingDuration) * Mathf.PI) * 95f : 0f;
            float side = stroke == StrokeKind.Backhand ? -1f : 1f;
            racketPivot.localPosition = new Vector3(side * 0.8f, 0.3f, 0.3f);
            racketPivot.localRotation = Quaternion.Euler(0f, side * arc, -side * arc * 0.25f);
            if (stroke == StrokeKind.Smash) racketPivot.localRotation = Quaternion.Euler(-arc, 0, 0);
            else if (stroke == StrokeKind.Volley) racketPivot.localRotation = Quaternion.Euler(0, side * arc * .35f, 10);
            else if (stroke == StrokeKind.Lob) racketPivot.localRotation = Quaternion.Euler(-arc * .5f, side * arc * .4f, 0);
            else if (stroke == StrokeKind.Slice) racketPivot.localRotation = Quaternion.Euler(45, side * arc * .5f, 30);
            // Keep the current hand pose attached during Update as well as after the articulated LateUpdate.
            if(athlete==null)athlete=GetComponentInChildren<AthleteMotion>();
            athlete?.LockGrip();
        }
    }
}
