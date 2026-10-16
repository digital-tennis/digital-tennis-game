using NUnit.Framework;
namespace DigitalTennis.Tests
{
    public sealed class TennisAudioPolicyTests
    {
        [TestCase(FrontEndStage.Playing)][TestCase(FrontEndStage.Pause)]
        public void ActiveMatchAlwaysMutesMusicEvenAtDeuceOrDuringSkill(FrontEndStage stage)
        {Assert.That(TennisAudioPolicy.MusicAllowed(stage),Is.False);}
        [TestCase("WaitServe")][TestCase("InFlight")][TestCase("Rally")]
        public void CrowdCannotCarryIntoServeOrRally(string phase)
        {Assert.That(TennisAudioPolicy.CrowdAllowed(FrontEndStage.Playing,phase),Is.False);Assert.That(TennisAudioPolicy.CrowdAllowed(FrontEndStage.Pause,phase),Is.False);}
        [Test]public void OnlyPointEndAndResultsAllowCrowdAndMenuMusicRemainsAvailable()
        {Assert.That(TennisAudioPolicy.CrowdAllowed(FrontEndStage.Playing,"PointEnd"),Is.True);Assert.That(TennisAudioPolicy.CrowdAllowed(FrontEndStage.Results,"WaitServe"),Is.True);Assert.That(TennisAudioPolicy.CrowdAllowed(FrontEndStage.Menu,"PointEnd"),Is.False);Assert.That(TennisAudioPolicy.MusicAllowed(FrontEndStage.Audio),Is.True);}
    }
}
