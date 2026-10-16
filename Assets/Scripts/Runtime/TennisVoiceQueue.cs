using System.Collections.Generic;
namespace DigitalTennis
{
    /// <summary>Bounded, expiring voice requests; official calls interrupt persona speech.</summary>
    public sealed class TennisVoiceQueue
    {
        private struct Request {public string Id;public float Until;}
        private readonly List<Request> pending=new List<Request>();
        private readonly Dictionary<string,float> cooldown=new Dictionary<string,float>();
        public void Reset(){pending.Clear();cooldown.Clear();}
        public static int Priority(string id)=>id.StartsWith("umpire.")?2:1;
        public bool Enqueue(string id,float now)
        {
            if(cooldown.TryGetValue(id,out float until)&&now<until)return false;
            cooldown[id]=now+(Priority(id)==2?.35f:2.5f);
            if(Priority(id)==2)pending.RemoveAll(r=>Priority(r.Id)<2);
            if(pending.Count>=6)return false;
            pending.Add(new Request{Id=id,Until=now+4});return true;
        }
        public string Take(float now,int activePriority)
        {
            pending.RemoveAll(r=>r.Until<now);
            int best=-1;for(int i=0;i<pending.Count;i++)if(Priority(pending[i].Id)>activePriority&&(best<0||Priority(pending[i].Id)>Priority(pending[best].Id)))best=i;
            if(best<0)return null;string id=pending[best].Id;pending.RemoveAt(best);return id;
        }
    }
}
