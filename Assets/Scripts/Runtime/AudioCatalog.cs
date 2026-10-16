using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DigitalTennis
{
    public enum AudioBus { Music, Voice, Sfx, Ambience }
    public sealed class AudioCue
    {
        public string Id,File,Source;
        public AudioBus Bus;
        public float Volume,Fade;
        public bool Loop;
    }
    public static class AudioCatalog
    {
        public const string Header="cue,group,file,volume,loop,fade,source";
        public static Dictionary<string,AudioCue> WithOptionalDefaults(string local,string shipped)
        {
            var result=Parse(local);var defaults=Parse(shipped);
            foreach(var pair in defaults)
                if((pair.Key.StartsWith("deuce_",StringComparison.Ordinal)||pair.Key.StartsWith("pressure_",StringComparison.Ordinal)||pair.Key.EndsWith("_alt",StringComparison.Ordinal)||pair.Key=="umpire.deuce"||pair.Key=="umpire.match_point")&&!result.ContainsKey(pair.Key))result.Add(pair.Key,pair.Value);
            if(result.Count>200)throw new FormatException("Merged audio catalog is too large.");return result;
        }
        public static Dictionary<string,AudioCue> Parse(string csv)
        {
            if(csv==null||csv.Length>131072)throw new FormatException("Audio CSV is too large.");
            var rows=CharacterBalanceCsv.ReadRows(csv.TrimStart('\ufeff'));
            if(rows.Count<7||rows.Count>201||string.Join(",",rows[0])!=Header)throw new FormatException("Audio CSV header or row count is invalid.");
            var cues=new Dictionary<string,AudioCue>(StringComparer.Ordinal);
            for(int i=1;i<rows.Count;i++)
            {
                var row=rows[i];if(row.Count!=7)throw new FormatException("Audio row needs seven fields: "+(i+1));
                string id=row[0],file=row[2];
                if(id.Length<1||id.Length>64||cues.ContainsKey(id))throw new FormatException("Invalid or duplicate audio cue: "+id);
                foreach(char c in id)if(!char.IsLetterOrDigit(c)&&c!='.'&&c!='_'&&c!='-')throw new FormatException("Invalid cue ID.");
                if(file.Length<1||file.Length>100||Path.IsPathRooted(file)||file!=Path.GetFileName(file)||file.Contains("..")||file.Contains("\\")||file.Contains(":"))throw new FormatException("Audio file must be a relative file name.");
                string extension=Path.GetExtension(file).ToLowerInvariant();if(extension!=".wav"&&extension!=".ogg")throw new FormatException("Only WAV/OGG files are supported.");
                if(!Enum.TryParse(row[1],out AudioBus bus)||!Enum.IsDefined(typeof(AudioBus),bus))throw new FormatException("Unknown audio group.");
                float volume=float.Parse(row[3],CultureInfo.InvariantCulture),fade=float.Parse(row[5],CultureInfo.InvariantCulture);
                if(!BallSimulation.IsFinite(volume)||volume<0||volume>1||!BallSimulation.IsFinite(fade)||fade<0||fade>10||(row[4]!="0"&&row[4]!="1"))throw new FormatException("Audio volume, fade or loop value is invalid.");
                if(row[4]=="1"&&(bus==AudioBus.Voice||bus==AudioBus.Sfx))throw new FormatException("Voice and one-shot effects cannot loop.");
                if(row[6].Length>500)throw new FormatException("Audio source note is too long.");
                cues.Add(id,new AudioCue{Id=id,File=file,Bus=bus,Volume=volume,Fade=fade,Loop=row[4]=="1",Source=row[6]});
            }
            foreach(string id in new[]{"menu_a","menu_b","game_a","game_b","result_a","result_b"})
                if(!cues.TryGetValue(id,out var cue)||cue.Bus!=AudioBus.Music)throw new FormatException("Missing music cue: "+id);
            return cues;
        }
    }
}
