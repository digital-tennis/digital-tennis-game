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
            int idsFlag = Array.IndexOf(args, "-dt-roster-ids");
            string[] requested = idsFlag >= 0 && idsFlag + 1 < args.Length ? args[idsFlag + 1].Split(',') : CharacterCatalog.Ids;
            foreach (string id in requested)
                if (Array.IndexOf(CharacterCatalog.Ids, id) < 0) throw new ArgumentException("Unknown preview athlete: " + id);
            int captured = 0;
            for (int i = 0; i < 15; i++)
            {
                if (Array.IndexOf(requested, CharacterCatalog.Ids[i]) < 0) continue;
                captured++;
                c.InputAdapter.Press(PlayerAction.SelectCharacter, false, i);
                c.Characters.Select(CourtSide.Far, (i + 1) % 15);
                c.InputAdapter.Press(PlayerAction.SelectCourt, false, i);
                // Preview-only staging: ordinary Skill input still validates and spends this one stock.
                c.Characters.Player(CourtSide.Near).Meter.Gain(100);
                c.InputAdapter.Press(PlayerAction.Skill);
                string id=CharacterCatalog.Ids[i];
                foreach(float phase in new[]{.18f,.5f,.80f})
                {
                    while(c.Characters.Player(CourtSide.Near).CinematicAge<phase*CharacterAbility.CinematicSeconds)yield return null;
                    yield return new WaitForEndOfFrame();GameCameraCapture.Save(Camera.main,Path.Combine(directory,id+"-phase-"+Mathf.RoundToInt(phase*100)+".png"));
                }
                yield return new WaitForEndOfFrame();
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
                yield return null;yield return null;yield return new WaitForEndOfFrame();
                var cameraAfter=Camera.main;position=cameraAfter.transform.position;rotation=cameraAfter.transform.rotation;fov=cameraAfter.fieldOfView;
                focus=c.NearAvatar.transform.position+Vector3.up*.35f;
                cameraAfter.transform.position=focus+c.NearAvatar.transform.forward*2.8f+Vector3.up*.15f;cameraAfter.transform.LookAt(focus);cameraAfter.fieldOfView=30;
                GameCameraCapture.Save(cameraAfter,Path.Combine(directory,name+"-face.png"));cameraAfter.transform.SetPositionAndRotation(position,rotation);cameraAfter.fieldOfView=fov;
            }
            Application.logMessageReceived -= Error;
            File.WriteAllText(Path.Combine(directory, "result.json"), "{\"mode\":\"staged-preview\",\"characters\":" + captured + ",\"runtimeErrors\":" + errors + ",\"points\":" + c.Match.Score.TotalPoints + "}");
            Application.Quit(errors == 0 && c.Match.Score.TotalPoints == 0 ? 0 : 1);
        }
        [Serializable] private sealed class Preview { public string mode; public CharacterProfile profile; public GamePayload state; }
    }
}
