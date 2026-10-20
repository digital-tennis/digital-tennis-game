using System;
using System.IO;
using DigitalTennis.AI;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Converts the live match for frozen, Unity-independent tactical inference.</summary>
    public sealed class FullMatchPolicyAdapter : IShotSelector
    {
        public const string Resource = "FullMatchAI/resumed-ppo";
        public static string OverridePath => Path.Combine(Application.persistentDataPath, "AI", "full-match-ppo.json");
        private readonly FullMatchActor actor;
        private float[] observation;
        public int Decisions { get; private set; }
        public int LastAction { get; private set; } = -1;
        public FullMatchPolicyAdapter(FullMatchActor actor) { this.actor = actor ?? throw new ArgumentNullException(nameof(actor)); }
        public void Observe(TennisMatchController controller) => observation = FullMatchObservation.Observe(controller, CourtSide.Far);
        public void Select(AiState state, out float width, out float depth)
        {
            LastAction = actor.Predict(observation);
            FullMatchActor.Placement(LastAction, out width, out depth);
            Decisions++;
        }
        public static FullMatchPolicyAdapter Parse(string json)
        {
            if (json == null || json.Length > 256 * 1024) throw new ArgumentException("Full-match model size");
            return new FullMatchPolicyAdapter(new FullMatchActor(JsonUtility.FromJson<FullMatchModelData>(json)));
        }
        public static FullMatchPolicyAdapter Load(out string status, string overridePath = null, string resource = Resource)
        {
            overridePath = overridePath ?? OverridePath;
            bool rejected = false;
            if (File.Exists(overridePath))
            {
                try
                {
                    if (new FileInfo(overridePath).Length > 256 * 1024) throw new ArgumentException("Full-match model size");
                    var custom = Parse(File.ReadAllText(overridePath)); status = "학습 AI · 전체 경기 교체 모델"; return custom;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                { rejected = true; Debug.LogWarning("TENNIS_FULL_MATCH override rejected: " + e.GetType().Name); }
            }
            try
            {
                var asset = Resources.Load<TextAsset>(resource);
                if (asset == null) throw new ArgumentException("Full-match model missing");
                var bundled = Parse(asset.text);
                status = rejected ? "학습 AI · 교체 오류 · 기본 모델" : "학습 AI";
                return bundled;
            }
            catch (ArgumentException e)
            { status = "기본 AI · 전체 경기 모델 오류"; Debug.LogWarning("TENNIS_FULL_MATCH fallback: " + e.GetType().Name); return null; }
        }
    }
}
