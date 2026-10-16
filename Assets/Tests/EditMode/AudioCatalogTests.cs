using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class AudioCatalogTests
    {
        private static string Csv=>File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Audio","audio.csv"));
        [Test] public void ShippedBankHasMusicPairsAndEveryPersonaVoiceAndRelativeFiles()
        {
            var cues=AudioCatalog.Parse(Csv);
            foreach(string id in CharacterCatalog.Ids)
                foreach(string cue in new[]{"ready","skill","win","lose"})Assert.That(cues.ContainsKey(id+"."+cue),Is.True);
            foreach(var cue in cues.Values)Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath,"Audio",cue.File)),Is.True,cue.Id);
        }
        [Test] public void OlderLocalCsvKeepsCustomVolumesAndGetsOptionalNewCues()
        {
            string[] rows=Csv.TrimEnd().Split('\n');string old=string.Join("\n",System.Array.FindAll(rows,r=>!r.StartsWith("deuce_")&&!r.StartsWith("pressure_")&&!r.Contains("_alt,")&&!r.StartsWith("umpire.deuce,")&&!r.StartsWith("umpire.match_point,")));
            old=old.Replace("game_a.wav,0.65","game_a.wav,0.21");var merged=AudioCatalog.WithOptionalDefaults(old,Csv);
            Assert.That(merged["game_a"].Volume,Is.EqualTo(.21f));Assert.That(merged.ContainsKey("deuce_a"));Assert.That(merged.ContainsKey("KOR.skill_alt"));
        }
        [TestCase("../outside.wav")][TestCase("/private.wav")][TestCase("C:\\secret.wav")][TestCase("https://example.com/a.wav")][TestCase("bad.mp3")]
        public void CatalogCannotReadArbitraryPathsOrUnsupportedFiles(string path)
        {Assert.Throws<FormatException>(()=>AudioCatalog.Parse(Csv.Replace("menu_a.wav",path)));}
        [TestCase("Voice")][TestCase("Sfx")]
        public void OneShotsCannotBlockTheVoiceQueueOrFillTheEffectsPoolWithLoops(string bus)
        {Assert.Throws<FormatException>(()=>AudioCatalog.Parse(Csv.TrimEnd()+"\ninvalid,"+bus+",voice.wav,0.5,1,0,original"));}
        [Test] public void InvalidReloadCannotRepresentNaNVolumeOrDuplicateCuesOrMissingMusic()
        {
            Assert.Throws<FormatException>(()=>AudioCatalog.Parse(Csv.Replace("menu_a,Music,menu_a.wav,0.65", "menu_a,Music,menu_a.wav,NaN")));
            Assert.Throws<FormatException>(()=>AudioCatalog.Parse(Csv.Replace("menu_b,", "menu_a,")));
            Assert.Throws<FormatException>(()=>AudioCatalog.Parse(Csv.Replace("menu_a,", "unused,")));
        }
    }
}
