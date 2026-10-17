using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.WebRTC;

namespace DigitalTennis
{
    public sealed class OnlinePeerTransport:MonoBehaviour
    {
        private RTCPeerConnection peer;private RTCDataChannel channel;
        private readonly Queue<RTCIceCandidateInit> earlyIce=new Queue<RTCIceCandidateInit>();
        private readonly Queue<string> incoming=new Queue<string>();
        private Action<string,PeerSignalData> signal;private Action opened,failed;
        private uint generation;private bool remoteSet,settingRemote,host,failureReported;
        public bool Ready=>channel!=null&&channel.ReadyState==RTCDataChannelState.Open;
        public bool Failed {get;private set;}
        public int Sent {get;private set;}public int Received {get;private set;}
        public void Begin(bool isHost,Action<string,PeerSignalData> sendSignal,Action onOpen,Action onFailure,string[] iceServers)
        {
            Close();host=isHost;signal=sendSignal;opened=onOpen;failed=onFailure;Failed=false;failureReported=false;Sent=Received=0;uint current=generation;
            try
            {
                var config=new RTCConfiguration{iceServers=iceServers==null||iceServers.Length==0?Array.Empty<RTCIceServer>():new[]{new RTCIceServer{urls=iceServers}}};
                peer=new RTCPeerConnection(ref config);
                peer.OnIceCandidate=c=>{if(current!=generation||string.IsNullOrEmpty(c.Candidate))return;signal("ice",new PeerSignalData{candidate=c.Candidate,sdpMid=c.SdpMid,sdpMLineIndex=c.SdpMLineIndex??0});};
                peer.OnConnectionStateChange=state=>{if(current==generation&&(state==RTCPeerConnectionState.Failed||state==RTCPeerConnectionState.Disconnected||state==RTCPeerConnectionState.Closed))Fail();};
                peer.OnDataChannel=c=>{if(current!=generation||channel!=null||host||c.Label!="digital-tennis-v1"){c.Close();c.Dispose();return;}Attach(c,current);};
                if(host){Attach(peer.CreateDataChannel("digital-tennis-v1",new RTCDataChannelInit{ordered=true}),current);StartCoroutine(Offer(current));}
            }
            catch(Exception e) when(e is DllNotFoundException||e is EntryPointNotFoundException||e is InvalidOperationException||e is NotSupportedException){Fail();}
        }
        private void Attach(RTCDataChannel value,uint current)
        {
            channel=value;channel.OnOpen=()=>{if(current==generation&&!Failed)opened?.Invoke();};channel.OnClose=()=>{if(current==generation)Fail();};
            channel.OnMessage=bytes=>
            {
                if(current!=generation||bytes==null||bytes.Length==0||bytes.Length>16384||incoming.Count>=64){if(current==generation)Fail();return;}
                try{incoming.Enqueue(new UTF8Encoding(false,true).GetString(bytes));Received++;}catch(DecoderFallbackException){Fail();}
            };
            if(channel.ReadyState==RTCDataChannelState.Open)opened?.Invoke();
        }
        private IEnumerator Offer(uint current)
        {
            var offer=peer.CreateOffer();yield return offer;if(current!=generation)yield break;if(offer.IsError){Fail();yield break;}
            var description=offer.Desc;var local=peer.SetLocalDescription(ref description);yield return local;if(current!=generation)yield break;if(local.IsError){Fail();yield break;}
            signal("offer",new PeerSignalData{type="offer",sdp=description.sdp});
        }
        public void Signal(string kind,PeerSignalData data)
        {
            if(peer==null||data==null||Failed)return;
            if(kind=="ice")
            {
                if(string.IsNullOrEmpty(data.candidate))return;
                if(data.candidate.Length>4096||data.sdpMLineIndex<0||data.sdpMLineIndex>255||data.sdpMid?.Length>256||earlyIce.Count>=128){Fail();return;}
                var candidate=new RTCIceCandidateInit{candidate=data.candidate,sdpMid=data.sdpMid,sdpMLineIndex=data.sdpMLineIndex};
                if(remoteSet)AddCandidate(candidate);else earlyIce.Enqueue(candidate);return;
            }
            if(kind!=(host?"answer":"offer")||data.type!=kind||string.IsNullOrEmpty(data.sdp)||data.sdp.Length>12000||settingRemote||remoteSet)return;
            settingRemote=true;StartCoroutine(RemoteDescription(kind,data.sdp,generation));
        }
        private IEnumerator RemoteDescription(string kind,string sdp,uint current)
        {
            var description=new RTCSessionDescription{type=kind=="offer"?RTCSdpType.Offer:RTCSdpType.Answer,sdp=sdp};
            var remote=peer.SetRemoteDescription(ref description);yield return remote;if(current!=generation)yield break;if(remote.IsError){Fail();yield break;}
            remoteSet=true;while(earlyIce.Count>0)AddCandidate(earlyIce.Dequeue());
            if(host)yield break;
            var answer=peer.CreateAnswer();yield return answer;if(current!=generation)yield break;if(answer.IsError){Fail();yield break;}
            description=answer.Desc;var local=peer.SetLocalDescription(ref description);yield return local;if(current!=generation)yield break;if(local.IsError){Fail();yield break;}
            signal("answer",new PeerSignalData{type="answer",sdp=description.sdp});
        }
        private void AddCandidate(RTCIceCandidateInit value){using(var candidate=new RTCIceCandidate(value)){if(!peer.AddIceCandidate(candidate))Fail();}}
        public bool Send(string message)
        {
            if(Failed||!Ready||message==null||Encoding.UTF8.GetByteCount(message)>16384||channel.BufferedAmount>256*1024){Fail();return false;}
            try{channel.Send(message);Sent++;return true;}catch(InvalidOperationException){Fail();return false;}
        }
        public bool TryRead(out string message){if(incoming.Count==0){message=null;return false;}message=incoming.Dequeue();return true;}
        private void Update(){if(peer!=null)WebRTC.ExecutePendingTasks(1);}
        private void Fail(){Failed=true;if(failureReported)return;failureReported=true;failed?.Invoke();}
        public void Close()
        {
            generation++;StopAllCoroutines();incoming.Clear();earlyIce.Clear();remoteSet=settingRemote=false;
            // The package also disposes its context on application quit. Release
            // our references first so either destruction order remains idempotent.
            var oldChannel=channel;channel=null;
            if(oldChannel!=null)
            {
                oldChannel.OnOpen=null;oldChannel.OnClose=null;oldChannel.OnMessage=null;
                try{oldChannel.Close();}catch(ObjectDisposedException){/* Package quit cleanup already closed it. */}
                finally{oldChannel.Dispose();}
            }
            var oldPeer=peer;peer=null;
            if(oldPeer!=null)
            {
                oldPeer.OnIceCandidate=null;oldPeer.OnConnectionStateChange=null;oldPeer.OnDataChannel=null;
                try{oldPeer.Close();}catch(ObjectDisposedException){/* Package quit cleanup already closed it. */}
                finally{oldPeer.Dispose();}
            }
            signal=null;opened=failed=null;
        }
        private void OnDestroy()=>Close();
    }
}
