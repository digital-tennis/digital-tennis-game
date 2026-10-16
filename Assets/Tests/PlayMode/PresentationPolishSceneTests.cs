using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class PresentationPolishSceneTests
    {
        private static IEnumerator Until(Func<bool> ready,float seconds=30)
        {
            double end=Time.realtimeSinceStartupAsDouble+seconds;
            while(!ready()&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.That(ready(),Is.True,"Ordinary demo input did not reach the required live presentation state.");
        }
        [UnityTest]
        public IEnumerator AnimatedHandsKeepTheRacketAttachedDuringRealRally()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();c.DemoReturnLimit=12;
            yield return Until(()=>c.NearHits>0&&c.FarHits>0);
            for(int frame=0;frame<20;frame++)
            {
                yield return null;
                foreach(var avatar in new[]{c.NearAvatar,c.FarAvatar})
                {
                    var rig=avatar.GetComponentInChildren<AthleteMotion>();Assert.That(rig,Is.Not.Null);
                    var hand=rig.transform.Find("Torso/RightArm/Elbow/Skin_Hand");
                    var pivot=avatar.transform.Find("Racket Pivot");
                    Assert.That(Vector3.Distance(hand.position,pivot.TransformPoint(new Vector3(0,.12f,0))),Is.LessThan(.035f));
                    Assert.That(rig.GetComponentsInChildren<Collider>(true),Is.Empty,"Art must not create gameplay hit boxes.");
                }
            }
            Assert.That(c.NearAvatar.DistanceTravelled+c.FarAvatar.DistanceTravelled,Is.GreaterThan(1));
        }
        [UnityTest]
        public IEnumerator RealContactsAlignTheStringBedWithoutMovingPhysicsOrPlayerRoots()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();c.DemoReturnLimit=12;
            int contacts=0;float worst=0,footError=0;long plants=0;
            c.SwingEvaluated+=(input,result)=>
            {
                if(result!=SwingResult.Hit)return;
                var actor=input.Player==CourtSide.Near?c.NearAvatar:c.FarAvatar;
                contacts++;worst=Mathf.Max(worst,Vector3.Distance(actor.StringCenter,c.Ball.Simulation.Position));
            };
            yield return Until(()=>c.NearHits>=2&&c.FarHits>=2);
            for(int i=0;i<30;i++)
            {
                yield return null;
                foreach(var a in new[]{c.NearAvatar,c.FarAvatar})
                {var rig=a.GetComponentInChildren<AthleteMotion>();footError=Mathf.Max(footError,rig.PlantedFootError);plants+=rig.PlantedFootSamples;}
            }
            Assert.That(contacts,Is.GreaterThanOrEqualTo(4));Assert.That(worst,Is.LessThan(.035f),"String bed must meet the authoritative contact, rather than peak later.");
            Assert.That(plants,Is.GreaterThan(0));Assert.That(footError,Is.LessThan(.035f),"Planted soles must meet the court.");
            Vector3 root=c.NearAvatar.transform.position,ball=c.Ball.Simulation.Position;
            c.NearAvatar.GetComponentInChildren<CharacterPresentation>().SynchronizeVisualPose();
            Assert.That(c.NearAvatar.transform.position,Is.EqualTo(root));Assert.That(c.Ball.Simulation.Position,Is.EqualTo(ball));
        }
        [UnityTest]
        public IEnumerator RecordedGestureUsesTheNormalSkillGateAndSpendsAnActuallyEarnedStock()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();c.DemoReturnLimit=12;
            yield return Until(()=>c.Characters.Player(CourtSide.Near).Meter.Stock>0);
            var catalog=SkillGestureCatalog.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"Balance","gestures.csv")));
            var source=new GestureSkillInputSource(new BufferedPlayerInputSource("fixture"),()=>c.Characters.Player(CourtSide.Near).Index,catalog);
            int before=c.Characters.Player(CourtSide.Near).Meter.Stock,activations=c.Characters.Activations;
            c.InputAdapter.UseSource(source);uint sequence=1;
            foreach(var token in catalog[CharacterCatalog.Ids[c.Characters.Player(CourtSide.Near).Index]].Tokens)source.PushToken(token,sequence++);
            yield return null;yield return null;
            Assert.That(c.Characters.Player(CourtSide.Near).Meter.Stock,Is.EqualTo(before-1));
            Assert.That(c.Characters.Activations,Is.EqualTo(activations+1));Assert.That(c.InputAdapter.LastAction,Is.EqualTo(PlayerAction.Skill));
        }
        [UnityTest]
        public IEnumerator SkillCameraRestoresTheCourtViewWithoutMovingGameplay()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();c.DemoReturnLimit=12;
            yield return Until(()=>c.Characters.Player(CourtSide.Near).Meter.Stock>0||c.Characters.Player(CourtSide.Far).Meter.Stock>0);
            var side=c.Characters.Player(CourtSide.Near).Meter.Stock>0?CourtSide.Near:CourtSide.Far;
            Vector3 basePosition=c.View.Camera.transform.position,ball=c.Ball.transform.position,near=c.NearAvatar.transform.position,far=c.FarAvatar.transform.position;
            Quaternion baseRotation=c.View.Camera.transform.rotation;ulong ticks=c.Ball.PhysicsTicks;
            c.InputAdapter.Press(PlayerAction.Skill,side==CourtSide.Far);
            yield return null;yield return null;
            Assert.That(Vector3.Distance(c.View.Camera.transform.position,basePosition),Is.GreaterThan(5));
            Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));Assert.That(c.Ball.transform.position,Is.EqualTo(ball));
            Assert.That(c.NearAvatar.transform.position,Is.EqualTo(near));Assert.That(c.FarAvatar.transform.position,Is.EqualTo(far));
            yield return Until(()=>!c.Characters.Cinematic,2);yield return null;
            Assert.That(Vector3.Distance(c.View.Camera.transform.position,basePosition),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(c.View.Camera.transform.rotation,baseRotation),Is.LessThan(.001f));
            yield return Until(()=>c.Ball.PhysicsTicks>ticks,2);
        }
    }
}
