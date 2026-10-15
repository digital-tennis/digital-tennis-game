using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Explicit staged art preview. Its granted stock is not evidence of gameplay charging.</summary>
    public sealed class CharacterShowcaseDriver : MonoBehaviour
    {
        private int errors;
        private void Error(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) errors++; }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "-dt-roster-preview");
            if (flag < 0 || flag + 1 >= args.Length) { enabled = false; yield break; }
            string directory = args[flag + 1]; Directory.CreateDirectory(directory);
            Application.targetFrameRate = 60; Application.runInBackground = true; Application.logMessageReceived += Error;
            yield return null;
            var c = GetComponent<TennisMatchController>(); c.AutoServe = c.DemoReturns = false;
            for (int i = 0; i < 15; i++)
            {
                c.InputAdapter.Press(PlayerAction.SelectCharacter, false, i);
                c.Characters.Select(CourtSide.Far, (i + 1) % 15);
                c.InputAdapter.Press(PlayerAction.SelectCourt, false, i);
                // Preview-only staging: ordinary Skill input still validates and spends this one stock.
                c.Characters.Player(CourtSide.Near).Meter.Gain(100);
                c.InputAdapter.Press(PlayerAction.Skill);
                yield return new WaitForSeconds(.58f); yield return new WaitForEndOfFrame();
                string name = CharacterCatalog.Ids[i]; GameCameraCapture.Save(Camera.main, Path.Combine(directory, name + ".png"));
                // An explicitly staged close-up of the same live rig pose, with the court view restored immediately.
                var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
                var focus = c.NearAvatar.transform.position + Vector3.up * .15f;
                camera.transform.position = focus + new Vector3(2.6f, 1.6f, -4);
                camera.transform.LookAt(focus); camera.fieldOfView = 35;
                GameCameraCapture.Save(camera, Path.Combine(directory, name + "-detail.png"));
                camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
                File.WriteAllText(Path.Combine(directory, name + ".json"), JsonUtility.ToJson(new Preview {
                    mode = "staged-character-and-skill-preview", profile = CharacterCatalog.Get(i), state = GamePayload.Capture(c, "preview", 0) }, true));
                while (c.Characters.Cinematic) yield return null;
            }
            Application.logMessageReceived -= Error;
            File.WriteAllText(Path.Combine(directory, "result.json"), "{\"mode\":\"staged-preview\",\"characters\":15,\"runtimeErrors\":" + errors + ",\"points\":" + c.Match.Score.TotalPoints + "}");
            Application.Quit(errors == 0 && c.Match.Score.TotalPoints == 0 ? 0 : 1);
        }
        [Serializable] private sealed class Preview { public string mode; public CharacterProfile profile; public GamePayload state; }
    }
}
