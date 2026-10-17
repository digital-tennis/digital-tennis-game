using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Original procedural character poses and ribbons; no licensed animations or gameplay transforms.</summary>
    // CharacterMatchSystem publishes the selected/buffered state at default order 0.
    // Apply its scale and skin before posing and locking the final visible grip.
    [DefaultExecutionOrder(100)]
    public sealed class CharacterPresentation : MonoBehaviour
    {
        [SerializeField] private Transform torso, leftArm, rightArm, head, model, racket;
        [SerializeField] private Renderer shirt, trousers, skin, trim, hair;
        [SerializeField] private LineRenderer[] ribbons;
        private int configured = -1;
        private CharacterState state;
        private AthleteMotion athlete;
        private readonly System.Collections.Generic.List<Material> owned = new System.Collections.Generic.List<Material>();
        private static readonly Vector4[] Poses = {
            new Vector4(-24,95,.14f,2), new Vector4(-12,-125,.08f,3), new Vector4(-32,75,.10f,4),
            new Vector4(-48,110,.24f,2), new Vector4(16,-70,.05f,5), new Vector4(-18,145,.09f,6),
            new Vector4(-8,60,.12f,4), new Vector4(-26,-100,.07f,3), new Vector4(-55,160,.25f,2),
            new Vector4(-20,-155,.13f,5), new Vector4(-36,85,.20f,3), new Vector4(-10,180,.12f,6),
            new Vector4(-30,-135,.16f,4), new Vector4(12,120,.06f,7), new Vector4(-42,-85,.18f,5) };
        public void Present(CharacterState value)
        {
            state = value;
            if(athlete==null)athlete=GetComponentInChildren<AthleteMotion>();
            athlete?.Present(value);
            if (configured == state.index) return;
            configured = state.index; var p = CharacterCatalog.Get(configured);
            Colorize(shirt, p.primary); Colorize(trousers, p.secondary); Colorize(skin, p.skin); Colorize(trim, p.accent); Colorize(hair, p.secondary);
            model.localScale = Vector3.one * (p.height / 1.75f);
            // Individual costume silhouette: headband, cap, or swept hair; no anatomy inferred from nationality.
            hair.transform.localScale = new Vector3(.34f + (p.style % 3) * .05f, .09f + (p.style % 4) * .025f, .37f);
            trim.transform.localRotation = Quaternion.Euler(0, 0, -25 + p.style * 4);
            foreach (var line in ribbons) { line.startColor = CharacterProfile.ColorOf(p.accent); line.endColor = CharacterProfile.ColorOf(p.primary); }
        }
        private void Colorize(Renderer renderer, string value)
        {
            var material = renderer.material;
            if (!owned.Contains(material)) owned.Add(material);
            material.color = CharacterProfile.ColorOf(value);
        }
        public void SynchronizeVisualPose()=>LateUpdate();
        private void LateUpdate()
        {
            if (state == null) return;

            bool cinematic = state.cinematicAge < CharacterAbility.CinematicSeconds;
            var pose = Poses[state.index];
            float t = state.cinematicAge / CharacterAbility.CinematicSeconds;
            float charge = Mathf.Sin(Mathf.Clamp01(t / .42f) * Mathf.PI * .5f);
            float release = t < .42f ? 0 : Mathf.Sin((t - .42f) / .58f * Mathf.PI);
            float power = cinematic ? Mathf.Sin(t * Mathf.PI) : 0;
            if(athlete==null)torso.localRotation = Quaternion.Euler(pose.x * power, pose.y * release, Mathf.Sin(t * Mathf.PI * 2) * power * 9);
            var authored=SkillArtCatalog.Sample(state.index,t);float amplitude=FrontEndController.Active?.ReduceMotion==true?.45f:1;
            model.localPosition = (cinematic?authored.Shift*amplitude:Vector3.zero)+Vector3.up*((athlete!=null?athlete.GroundedModelY:0)+(cinematic?authored.Jump*amplitude:0));
            athlete?.Pose(state);
            if(athlete==null)
            {
            leftArm.localRotation = Quaternion.Euler(-25 * power, 0, -25 - 100 * power);
            rightArm.localRotation = Quaternion.Euler(pose.x * power, -pose.y * release, 25 + 105 * charge * power);
            head.localRotation = Quaternion.Euler(-8 * power, -pose.y * release * .25f, 0);
            if (cinematic) racket.localRotation = Quaternion.Euler(-90 * charge, pose.y * release, -65 * power);
            }
            for (int r = 0; r < ribbons.Length; r++)
            {
                var line=ribbons[r];float envelope=cinematic?Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI),2):0;
                int visible=FrontEndController.Active?.ReduceMotion==true?1:3;
                line.enabled=cinematic&&r<visible&&envelope>.015f;
                if(!line.enabled)continue;
                var profile=CharacterCatalog.Get(state.index);var start=CharacterProfile.ColorOf(profile.accent);var end=CharacterProfile.ColorOf(profile.primary);
                start.a=.55f*envelope;end.a=.12f*envelope;line.startColor=start;line.endColor=end;line.widthMultiplier=.012f+r*.002f;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float u=(float)i/(line.positionCount-1);float effectTime=FrontEndController.Active?.ReduceMotion==true?.5f:t;
                    line.SetPosition(i,SkillArtCatalog.Ribbon(state.index,r,u,effectTime));
                }
            }
            athlete?.CompleteDetailedPose(state);
            athlete?.LockGrip();
        }
        private void OnDestroy() { foreach (var m in owned) if (m != null) Destroy(m); }
    }
}
