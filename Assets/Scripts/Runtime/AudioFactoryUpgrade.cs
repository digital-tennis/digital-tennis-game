using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Upgrade only byte-identical factory recordings. Local files and CSV values are never written.</summary>
    public static class AudioFactoryUpgrade
    {
        [Serializable] public sealed class Entry { public string id,file,sha256,caption; }
        [Serializable] public sealed class Manifest { public Entry[] entries; }
        public static HashSet<string> Apply(Dictionary<string,AudioCue> cues,Dictionary<string,AudioCue> defaults,string manifest,string directory)
        {
            var upgraded=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in JsonUtility.FromJson<Manifest>(manifest).entries)
            {
                if(!cues.TryGetValue(entry.id,out var cue)||!defaults.TryGetValue(entry.id,out var factory)||cue.File!=entry.file)continue;
                // Validate names through the parsed catalogs; a manifest may not open an arbitrary path.
                string path=Path.Combine(directory,cue.File);if(!File.Exists(path))continue;
                if(new FileInfo(path).Length>32*1024*1024)continue;
                string hash;using(var sha=SHA256.Create())using(var input=File.OpenRead(path))hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
                if(hash!=entry.sha256)continue;
                cue.File=factory.File;cue.Source=factory.Source;cue.FactoryFile=true;upgraded.Add(entry.id);
            }
            return upgraded;
        }
        public static void MergeCaptions(Dictionary<string,string> result,string local,string manifest,HashSet<string> upgraded)
        {
            var old=new Dictionary<string,string>();foreach(var entry in JsonUtility.FromJson<Manifest>(manifest).entries)old[entry.id]=entry.caption;
            var rows=CharacterBalanceCsv.ReadRows(local.TrimStart('\ufeff'));
            if(rows.Count>500)throw new FormatException("Subtitle catalog is too large.");
            foreach(var row in rows)
                if(row.Count==3&&row[1]=="ko"&&!(upgraded.Contains(row[0])&&old.TryGetValue(row[0],out var text)&&text==row[2]))result[row[0]]=row[2];
        }
    }
}
