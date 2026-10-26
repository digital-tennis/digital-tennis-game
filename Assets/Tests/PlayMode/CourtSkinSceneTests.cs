using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class CourtSkinSceneTests
    {
        private const string PreferenceKey = "DigitalTennis.CourtSkin";
        private CourtSkinStyle previous;
        private bool hadPreference;
        private int preference;

        [SetUp] public void SavePreference()
        {
            previous = CourtSkin.Current;
            hadPreference = PlayerPrefs.HasKey(PreferenceKey);
            preference = PlayerPrefs.GetInt(PreferenceKey);
        }

        [TearDown] public void RestorePreference()
        {
            CourtSkin.Apply(previous);
            if (hadPreference) PlayerPrefs.SetInt(PreferenceKey, preference);
            else PlayerPrefs.DeleteKey(PreferenceKey);
            PlayerPrefs.Save();
        }

        private static IEnumerator Until(Func<bool> predicate)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, "FrontEnd did not reach the expected state.");
        }

        [UnityTest]
        public IEnumerator SettingsToggleAndAllCourtsRestoreWithoutChangingMatch()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");
            yield return Until(() => FrontEndController.Active?.Ready == true);
            var flow = FrontEndController.Active;
            flow.Match.InputAdapter.ConfigureDevices(new UnavailableControllerBackend(), new MemoryInputDevicePreference());
            if (flow.Model.Stage == FrontEndStage.Logo) flow.Confirm();
            flow.Confirm();
            flow.MenuChoice(3);
            Assert.That(flow.Model.Stage, Is.EqualTo(FrontEndStage.Options));
            var initial = CourtSkin.Current;
            Assert.That(flow.View.Invoke("court.skin"), Is.True);
            Assert.That(CourtSkin.Current, Is.Not.EqualTo(initial));
            Assert.That(flow.View.Invoke("court.skin"), Is.True);
            Assert.That(CourtSkin.Current, Is.EqualTo(initial));
            if (Application.isBatchMode)
            {
                Assert.That(PlayerPrefs.HasKey(PreferenceKey), Is.EqualTo(hadPreference));
                Assert.That(PlayerPrefs.GetInt(PreferenceKey), Is.EqualTo(preference));
            }
            flow.Back();
            flow.MenuChoice(0);
            flow.Confirm(); flow.Confirm(); flow.Confirm();
            yield return Until(() => flow.Model.Stage == FrontEndStage.Playing);
            var match = flow.Match;
            match.IsPaused = true;
            var court = GameObject.Find("Court").GetComponent<Renderer>();
            var surround = GameObject.Find("Surround").GetComponent<Renderer>();
            var stadium = UnityEngine.Object.FindFirstObjectByType<TennisStadium>();
            var seats = stadium.transform.Find("Individual Stadium Seats").GetComponent<Renderer>();
            var board = stadium.transform.Find("Live Score -1").GetComponent<TextMesh>();
            var property = new MaterialPropertyBlock();
            ulong ticks = match.Ball.PhysicsTicks;
            var position = match.Ball.Simulation.Position;
            long points = match.Match.Progress.TotalPoints;
            uint shot = match.Match.CurrentShot.ShotId;
            var surfaces = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < CharacterCatalog.Ids.Length; i++)
            {
                Assert.That(match.Characters.SelectCourt(i), Is.True);
                var profile = CharacterCatalog.Get(i);
                CourtSkin.Apply(CourtSkinStyle.Original);
                seats.GetPropertyBlock(property);
                Color originalSeats = property.GetColor("_Color"), originalBoard = board.color;
                Color originalCourt = court.sharedMaterial.color, originalSurround = surround.sharedMaterial.color;
                Assert.That(originalCourt, Is.EqualTo(CharacterProfile.ColorOf(profile.courtColor)));
                var shader = court.sharedMaterial.shader;
                float surface = court.sharedMaterial.GetFloat("_Surface");
                CourtSkin.Apply(CourtSkinStyle.Club);
                flow.RefreshView();
                Assert.That(court.sharedMaterial.color, Is.Not.EqualTo(originalCourt), profile.id);
                Assert.That(court.sharedMaterial.shader, Is.SameAs(shader));
                Assert.That(court.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(surface));
                Assert.That(match.Characters.CourtIndex, Is.EqualTo(i));
                yield return null;
                bool capture = surfaces.Add(profile.surface);
                if (capture) Capture(flow, profile.surface + "-club");
                CourtSkin.Apply(CourtSkinStyle.Original);
                flow.RefreshView();
                seats.GetPropertyBlock(property);
                Assert.That(court.sharedMaterial.color, Is.EqualTo(originalCourt));
                Assert.That(surround.sharedMaterial.color, Is.EqualTo(originalSurround));
                Assert.That(property.GetColor("_Color"), Is.EqualTo(originalSeats));
                Assert.That(board.color, Is.EqualTo(originalBoard));
                yield return null;
                if (capture) Capture(flow, profile.surface + "-original");
            }
            Assert.That(surfaces, Is.EquivalentTo(new[] { "hard", "clay", "grass" }));
            Assert.That(match.Ball.PhysicsTicks, Is.EqualTo(ticks));
            Assert.That(match.Ball.Simulation.Position, Is.EqualTo(position));
            Assert.That(match.Match.Progress.TotalPoints, Is.EqualTo(points));
            Assert.That(match.Match.CurrentShot.ShotId, Is.EqualTo(shot));
        }

        // Opt-in real Unity rendering. Never substitutes an illustrative image for a game capture.
        private static void Capture(FrontEndController flow, string name)
        {
            if (!FrontEndController.HasArgument("-dt-court-skin-captures")) return;
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
                "Capture requires graphics: use tools/unity.sh test-skin-visual.");
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var world = flow.Match.View.Camera;
            var oldWorld = world.targetTexture;
            var oldUi = flow.UiCamera.targetTexture;
            var active = RenderTexture.active;
            try
            {
                target.Create();
                world.targetTexture = target; flow.UiCamera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); world.Render(); flow.UiCamera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../.work/court-skin"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                world.targetTexture = oldWorld; flow.UiCamera.targetTexture = oldUi;
                RenderTexture.active = active;
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
