using UnityEngine;

namespace DigitalTennis
{
    public enum CourtSkinStyle { Original, Club }

    /// <summary>Local cosmetic preference. Never changes the selected court or match state.</summary>
    public static class CourtSkin
    {
        private const string PreferenceKey = "DigitalTennis.CourtSkin";
        private static CourtSkinStyle? current;
        public static CourtSkinStyle Current => current ?? (current = ReadPreference()).Value;
        public static bool IsClub => Current == CourtSkinStyle.Club;
        public static Color Cream => CharacterProfile.ColorOf("#EFE8D2");
        public static Color HudPanel => IsClub ? new Color(.055f,.14f,.115f,.96f) : new Color(.025f,.065f,.09f,.94f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;

        private static CourtSkinStyle ReadPreference()
        {
            // Batch validation must not depend on, or write to, a developer's preferences.
            int value = Application.isBatchMode ? 1 : PlayerPrefs.GetInt(PreferenceKey, 1);
            return value == 0 ? CourtSkinStyle.Original : CourtSkinStyle.Club;
        }

        public static void Apply(CourtSkinStyle style)
        {
            if (style != CourtSkinStyle.Original && style != CourtSkinStyle.Club) return;
            current = style;
            foreach (var court in Object.FindObjectsByType<NationalCourtView>(FindObjectsSortMode.None)) court.RefreshSkin();
            foreach (var stadium in Object.FindObjectsByType<TennisStadium>(FindObjectsSortMode.None)) stadium.RefreshSkin();
            if (!Application.isBatchMode)
            {
                PlayerPrefs.SetInt(PreferenceKey, (int)style);
                PlayerPrefs.Save();
            }
        }

        public static Color Surface(CharacterProfile profile)
        {
            if (!IsClub) return CharacterProfile.ColorOf(profile.courtColor);
            string color = profile.surface == "clay" ? "#B77453" : profile.surface == "grass" ? "#527C51" : "#3F7F70";
            return Color.Lerp(CharacterProfile.ColorOf(color), CharacterProfile.ColorOf(profile.courtColor), .15f);
        }

        public static Color Surround(CharacterProfile profile) => IsClub
            ? CharacterProfile.ColorOf("#173B34")
            : Color.Lerp(CharacterProfile.ColorOf(profile.sky), Color.black, .25f);
    }
}
