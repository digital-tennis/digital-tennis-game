using System;
using System.IO;
using DigitalTennis.AI;
using UnityEngine;

namespace DigitalTennis
{
    public static class TennisLearnedPolicy
    {
        public static string OverridePath => Path.Combine(Application.persistentDataPath, "AI", "placement-ppo.json");
        public static LearnedShotSelector Parse(string json)
        {
            if (json == null || json.Length > 256 * 1024) throw new ArgumentException("AI model size is invalid.");
            return new LearnedShotSelector(JsonUtility.FromJson<PpoModelData>(json));
        }
        public static LearnedShotSelector Load(out string status)
        {
            string warning = null;
            if (File.Exists(OverridePath))
            {
                try
                {
                    if (new FileInfo(OverridePath).Length > 256 * 1024) throw new ArgumentException("AI model is too large.");
                    var custom = Parse(File.ReadAllText(OverridePath)); status = "학습 AI · 교체 모델"; return custom;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                { warning = "교체 모델 오류 · 기본 모델 사용"; Debug.LogWarning("TENNIS_AI_MODEL override rejected: " + e.GetType().Name); }
            }
            try
            {
                var asset = Resources.Load<TextAsset>("DigitalTennisAI/placement-ppo");
                if (asset == null) throw new ArgumentException("Bundled AI model is missing.");
                var bundled = Parse(asset.text); status = warning ?? "학습 AI"; return bundled;
            }
            catch (ArgumentException e)
            {
                status = "기본 AI · 학습 모델 오류";
                Debug.LogWarning("TENNIS_AI_MODEL fallback: " + e.Message); return null;
            }
        }
    }
}
