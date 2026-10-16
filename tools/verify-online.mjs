#!/usr/bin/env node
// Run two real standalone Unity processes against the sibling local server.
import { spawn } from 'node:child_process';
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
function arg(name, fallback) { const i = args.indexOf(name); return i < 0 ? fallback : args[i + 1]; }
const serverRepo = path.resolve(arg('--server-repo', path.join(root, '..', 'digital-tennis-match-server')));
const player = path.resolve(arg('--player', path.join(root, 'Builds/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis')));
const hostPlayer = path.resolve(arg('--host-player', player));
const guestPlayer = path.resolve(arg('--guest-player', player));
const out = path.resolve(arg('--out', path.join(root, '.work/online')));
await fs.access(player);
await fs.mkdir(out, { recursive: true });
const rulesFile=path.join(out,'validation-rules.csv');
await fs.copyFile(path.join(root,'Assets/StreamingAssets/Balance/rules.csv'),rulesFile);
const codeFile = path.join(out, 'room-code.txt');
await fs.rm(codeFile, { force: true });
await fs.rm(path.join(out, 'combined.json'), { force: true });
// A rerun must prove fresh frames, including menu stages and particle captures.
for (const name of await fs.readdir(out)) {
  if (/^(host|guest)-view(?:-.*)?\.(png|json)$/.test(name)) await fs.rm(path.join(out, name));
}
for (const role of ['host', 'guest']) {
  await fs.rm(path.join(out, `${role}.json`), { force: true });
}
const { startServer } = await import(pathToFileURL(path.join(serverRepo, 'src/server.js')));
const server = await startServer({ port: 0 });
const children = [];
let reconnected = false;
const exerciseReconnect = args.includes('--exercise-reconnect');
const exerciseOfficials = args.includes('--exercise-officials');
const exerciseCharacters = args.includes('--exercise-characters');
const menus = args.includes('--menus');
const exerciseEffects = args.includes('--exercise-effects');
const exerciseBalance=args.includes('--exercise-balance');
const exerciseRematch=args.includes('--exercise-rematch');
const exerciseMisses=args.includes('--exercise-misses');
assert.ok(!exerciseRematch||menus,'Rematch proof must include the actual result menu');
assert.ok(!exerciseBalance||exerciseCharacters,'Bundle reload proof uses the existing safe character reload route');
assert.ok(!exerciseEffects || (exerciseCharacters && exerciseReconnect), 'Effects proof includes real skill charging and reconnect');
// Always isolate verification from the user's editable balance file.
const balanceFile = path.join(out, 'validation-balance.csv');
const defaultBalance = await fs.readFile(path.join(root, 'Assets/StreamingAssets/Balance/characters.csv'), 'utf8');
await fs.writeFile(balanceFile, defaultBalance);
let balanceChanged = false;
const gameplayFile=path.join(out,'validation-gameplay.csv');
const defaultGameplay=await fs.readFile(path.join(root,'Assets/StreamingAssets/Balance/gameplay.csv'),'utf8');
await fs.writeFile(gameplayFile,defaultGameplay);
const guestGameplayFile=path.join(out,'guest-gameplay.csv');
await fs.writeFile(guestGameplayFile,defaultGameplay.replace('move.speed,5,','move.speed,8,'));
const effectsFile=path.join(out,'validation-effects.csv');
const defaultEffects=await fs.readFile(path.join(root,'Assets/StreamingAssets/Effects/effects.csv'),'utf8');
await fs.writeFile(effectsFile,defaultEffects);
let effectsChanged=false;
const headless = args.includes('--headless');
const offscreen = args.includes('--offscreen');
assert.ok(!(headless && offscreen), 'Choose either --headless (no GPU) or --offscreen (GPU camera PNGs)');
for(const role of ['host','guest'])await fs.cp(path.join(root,'Assets/StreamingAssets/Audio'),path.join(out,'audio',role),{recursive:true,filter:source=>!source.endsWith('.meta')});
const monitor = setInterval(async () => {
  const room = [...server.rooms.rooms.values()][0];
  console.log(`Online progress: ${room?.state ?? 'waiting'} epoch=${room?.epoch ?? 0} relay=${server.rooms.relayCount}`);
  if(exerciseEffects&&!effectsChanged&&room?.state==='PLAYING'&&server.rooms.relayCount>=90) {
    effectsChanged=true;
    await fs.writeFile(`${effectsFile}.pending`,defaultEffects.replace('hit,1,18,','hit,1,26,'));
    await fs.rename(`${effectsFile}.pending`,effectsFile);
    console.log('Verification: changed isolated particle CSV; both clients must reload locally through F5');
  }
  if (exerciseCharacters && !balanceChanged && room?.state === 'PLAYING' && server.rooms.relayCount >= 240) {
    balanceChanged = true;
    const modified = defaultBalance.replace(/^KOR,70,/m, 'KOR,77,');
    assert.notEqual(modified, defaultBalance);
    await fs.writeFile(`${balanceFile}.pending`, modified);
    if(exerciseBalance){await fs.writeFile(gameplayFile+'.pending',defaultGameplay.replace('move.speed,5,','move.speed,6,'));await fs.rename(gameplayFile+'.pending',gameplayFile);}
    await fs.rename(`${balanceFile}.pending`, balanceFile);
    console.log('Verification: changed isolated balance bundle; host must reload through the normal input adapter');
  }
  if (exerciseReconnect && !reconnected && room?.state === 'PLAYING' && server.rooms.relayCount >= 180) {
    reconnected = true; room.players.guest.client.terminate();
    if(exerciseEffects) {
      // Reproduce a server response to a snapshot in flight at disconnect, over the real host socket.
      room.players.host.client.send(JSON.stringify({v:1,type:'error',requestId:'verification-disconnect-race',payload:{code:'PEER_UNAVAILABLE',message:'Peer unavailable during reconnect'}}));
    }
    console.log('Verification: interrupted guest socket once; waiting for token resume and epoch 2');
  }
}, 10000);
try {
  const launch = role => {
    const child = spawn(role === 'host' ? hostPlayer : guestPlayer, [...(headless ? ['-batchmode', '-nographics'] : offscreen ? ['-batchmode'] : []),
      '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '800',
      '-dt-online-role', role, '-dt-server', server.url, '-dt-code-file', codeFile,
      '-dt-report', path.join(out, `${role}.json`), ...(!headless ? ['-dt-view-image', path.join(out, `${role}-view.png`)] : []),
      '-dt-metrics-dir',path.join(out,'metrics',role),'-dt-timeout', '900','-dt-audio-file',path.join(out,'audio',role,'audio.csv'),'-dt-audio-validation',...(exerciseRematch?['-dt-rematch']:[]),...(exerciseMisses?['-dt-misses']:[]), '-dt-rules-file',rulesFile, '-dt-gameplay-file',exerciseBalance&&role==='guest'?guestGameplayFile:gameplayFile,'-dt-balance-file', balanceFile, '-dt-effects-file',effectsFile,'-logFile', path.join(out, `${role}.log`),
      ...(exerciseOfficials ? ['-dt-officials'] : []), ...(exerciseCharacters ? ['-dt-characters'] : []), ...(menus ? ['-dt-menu-online'] : []),...(exerciseEffects?['-dt-effects']:[])], { stdio: 'ignore' });
    children.push(child); console.log(`Started ${role} Unity PID ${child.pid}`);
    return new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', code => resolve({ role, code, pid: child.pid })); });
  };
  const hostDone = launch('host'), guestDone = launch('guest');
  const results = await Promise.all([hostDone, guestDone]);
  await fs.writeFile(path.join(out, 'processes.json'), JSON.stringify(results, null, 2));
  for (const result of results) assert.equal(result.code, 0, `${result.role} exited before a valid report; inspect its log and processes.json`);
  const host = JSON.parse(await fs.readFile(path.join(out, 'host.json'))), guest = JSON.parse(await fs.readFile(path.join(out, 'guest.json')));
  await fs.writeFile(path.join(out, 'combined.json'), JSON.stringify({ rendering: headless ? 'headless' : offscreen ? 'offscreen-camera' : 'windowed', processes: results, relayedMessages: server.rooms.relayCount, host, guest }, null, 2));
  for (const result of results) assert.equal(result.code, 0, `${result.role} validation failed; inspect its log/report`);
  assert.notEqual(results[0].pid, results[1].pid);
  if (!headless) { await fs.access(path.join(out, 'host-view.png')); await fs.access(path.join(out, 'guest-view.png')); }
  for (const report of [host, guest]) {
    assert.equal(report.captureMode, headless ? 'none' : offscreen ? (menus ? 'camera-canvas' : 'camera') : 'screen');
    if (menus) {
      assert.ok(report.menuTransitions >= 6);
      assert.equal(report.menuClickFailures, 0, report.lastMenuPointerProblem ?? 'Menu clicks must succeed');
      const selectsCourt = report.role === 'host' && exerciseCharacters;
      const country = report.role === 'host' ? 'KOR' : exerciseCharacters ? 'ESP' : 'JPN';
      const expectedClicks = ['continue', 'menu.online', `country.${country}`, 'confirm',
        ...(selectsCourt ? ['lobby.court', 'country.ESP', 'confirm'] : []),
        report.role === 'host' ? 'lobby.create' : 'lobby.join',...(exerciseRematch?['rematch']:[])];
      assert.deepEqual(report.menuClicks, expectedClicks, 'Selection must be clicked once, then confirmed on a later frame');
      if (!headless && !offscreen) {
        assert.equal(report.menuCallbackActivations, 0, 'Normal windows must use UI raycasts, never callback fallback');
        assert.equal(report.menuPointerActivations, expectedClicks.length);
      } else assert.equal(report.menuCallbackActivations, expectedClicks.length);
      const stages = ['Logo', 'Title', 'Menu', 'Player', 'Lobby', ...(selectsCourt ? ['Court'] : []), 'Versus', 'Playing'];
      let previousStage = -1;
      for (const stage of stages) {
        const index = report.menuStages.indexOf(stage);
        assert.ok(index > previousStage, `Missing or out-of-order menu stage: ${stage}`); previousStage = index;
        if (!headless) {
          const capture = path.join(out, `${report.role}-view-menu-${stage.toLowerCase()}`);
          const frame = JSON.parse(await fs.readFile(capture + '.json', 'utf8'));
          assert.equal(frame.stage, stage); assert.equal(frame.role, report.role);
          assert.equal(frame.source, offscreen ? 'game-cameras-with-canvas' : 'game-frame-with-canvas');
          if (stage === 'Player') assert.equal(frame.focus, report.role === 'host' ? 0 : exerciseCharacters ? 8 : 1);
          if (stage === 'Court') assert.equal(frame.focus, 8);
          const png = await fs.readFile(capture + '.png');
          assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
          assert.equal(png.readUInt32BE(16), offscreen ? 1600 : 1280);
          assert.equal(png.readUInt32BE(20), offscreen ? 900 : 800);
        }
      }
    }
  }
  if(exerciseBalance){assert.equal(host.moveSpeed,6);assert.equal(guest.moveSpeed,6);assert.equal(host.gameplayHash,guest.gameplayHash);assert.match(await fs.readFile(guestGameplayFile,'utf8'),/move.speed,8,/);}
  if(exerciseRematch){
    for(const report of [host,guest]){assert.equal(report.rematches,1);assert.ok(report.sameRoom&&report.singleVoteWaited&&report.rematchRally);assert.notEqual(report.firstMatchId,report.newMatchId);assert.ok(report.rematchPoints.reduce((a,b)=>a+b,0)>0);}
    assert.equal(host.firstMatchId,guest.firstMatchId);assert.equal(host.newMatchId,guest.newMatchId);assert.deepEqual(host.rematchPoints,guest.rematchPoints);
  }
  if(exerciseMisses)for(const report of [host,guest])assert.ok(report.missesNear>0&&report.missesFar>0,'Both clients must present fresh misses from both players');
  assert.deepEqual(host.points, guest.points); assert.equal(host.winner, guest.winner);
  assert.deepEqual(host.score,guest.score);assert.deepEqual(host.score.sets,[2,0]);
  assert.deepEqual(host.score.completedSets.map(s=>[s.nearGames,s.farGames]),[[7,6],[6,0]]);
  assert.deepEqual([host.score.completedSets[0].nearTieBreak,host.score.completedSets[0].farTieBreak],[8,6]);
  for(const report of [host,guest]) {
    assert.ok(report.completedPoints>=86);assert.ok(report.score.endChanges>=10);
    if(!headless)for(const milestone of ['next-game','tiebreak-start','tiebreak-deuce','set-result','next-set','match-result']) {
      const capture=path.join(out,`${report.role}-view-${milestone}`);await fs.access(capture+'.png');
      const record=JSON.parse(await fs.readFile(capture+'.json','utf8'));assert.ok(record.state.score);
    }
  }
  if(exerciseEffects) {
    assert.ok(effectsChanged);
    for(const report of [host,guest]) {
      assert.equal(report.effects.duplicateErrors,0);assert.equal(report.effects.hitCount,26);assert.ok(report.effects.csvRevision>=3);
      assert.ok(report.effects.kinds.every(n=>n>0));
      if(!headless) {
        assert.equal(report.effects.poolCreated,12);assert.ok(report.effects.rendered>0&&report.effects.peakParticles>0&&report.effects.peakParticles<=1536);
        for(const name of ['serve','hit','bounce','charge','skill']) {
          const capture=path.join(out,`${report.role}-view-fx-${name}`);await fs.access(capture+'.png');
          const record=JSON.parse(await fs.readFile(capture+'.json','utf8'));assert.ok(record.activeEffects>0&&record.liveParticles>0);
        }
      }
    }
    assert.ok(guest.effects.remoteBaselines>=2);
    assert.ok(host.transientPeerPauses>=1,'Host must pause/resume a real peer-unavailable response without treating it as malformed gameplay');
  }
  assert.equal(host.epoch, guest.epoch); assert.equal(guest.physicsTicks, 0); assert.ok(host.physicsTicks > 0);
  assert.ok(server.rooms.relayCount > 0);
  assert.equal(host.view.localPlayer, 'Near'); assert.equal(guest.view.localPlayer, 'Far');
  assert.ok((host.score.endChanges%2===0?host.view.cameraPosition[2]:-host.view.cameraPosition[2])<0);
  assert.ok((guest.score.endChanges%2===0?guest.view.cameraPosition[2]:-guest.view.cameraPosition[2])>0);
  for (const report of [host, guest]) {
    assert.ok(report.view.frames > 30); assert.equal(report.view.viewErrors, 0);
    assert.ok(report.view.opponentRightMoves > 0 && report.view.opponentLeftMoves > 0);
    assert.equal(report.view.mirroredMovementErrors, 0);
  }
  assert.ok(guest.view.worldPoseChecks > 0 && guest.view.strokeChecks > 0);
  assert.equal(guest.view.worldPoseErrors, 0); assert.equal(guest.view.strokeErrors, 0);
  assert.ok(guest.view.opponentForehands > 0 && guest.view.opponentBackhands > 0);
  assert.ok(guest.view.localForehands > 0 && guest.view.localBackhands > 0);
  if (exerciseReconnect) { assert.ok(reconnected); assert.ok(host.epoch >= 2); }
  if (exerciseCharacters) {
    assert.ok(balanceChanged);
    for (const report of [host, guest]) {
      assert.equal(report.characters[0].index, 0); assert.equal(report.characters[1].index, 8); assert.equal(report.courtIndex, 8);
      assert.equal(report.characters[0].balance.serve, 77); assert.ok(report.balanceRevision >= 3);
      assert.ok(report.characters.every(p => p.activationId >= 1 && p.stock >= 0 && p.stock <= 3));
      assert.deepEqual(report.maxStocks, [3, 3]);
      assert.ok(report.cinematicFrames > 0); assert.ok(report.characterChecks > 30); assert.equal(report.characterErrors, 0);
    }
    // Reports finish at different wall times; compare persistent state, while each guest frame
    // independently checks its displayed skill state against the buffered authoritative snapshot.
    const persistent = p => ({ index: p.index, stock: p.stock, gauge: p.gauge, activationId: p.activationId, balance: p.balance });
    assert.deepEqual(host.characters.map(persistent), guest.characters.map(persistent));
    if (!headless) for (const role of ['host', 'guest']) await fs.access(path.join(out, `${role}-view-skill.png`));
  }
  if (exerciseOfficials) {
    for (const report of [host, guest]) {
      assert.ok(report.completedRecoveries >= 3); assert.ok(report.recoveryFrames > 0);
      assert.equal(report.recoveryStages, 7); assert.equal(report.officialsCalls, 7); assert.equal(report.officialsErrors, 0);
    }
    assert.ok(guest.officialsChecks > 0);
    if (!headless) {
      for (const role of ['host', 'guest']) {
        for (const suffix of ['recovery', 'carry']) {
          await fs.access(path.join(out, `${role}-view-${suffix}.png`));
          const capture = JSON.parse(await fs.readFile(path.join(out, `${role}-view-${suffix}.json`)));
          assert.equal(capture.state.officials.recovery, suffix === 'recovery' ? 2 : 3);
          assert.equal(capture.role, role);
        }
      }
    }
  }
  console.log(`PASS: two Unity processes, sets=${host.score.sets.join('-')} games=${host.score.completedSets.map(s=>s.nearGames+'-'+s.farGames).join('/')}, winner=${host.winner}, rally=${host.maxRallyHits}, relay=${server.rooms.relayCount}`);
  if (menus) console.log(`Menu proof: ${host.menuPointerActivations}/${guest.menuPointerActivations} pointer clicks, ${host.menuCallbackActivations}/${guest.menuCallbackActivations} callbacks; both reached Playing through the menu`);
  console.log(`Reports: ${out}`);
} finally {
  clearInterval(monitor);
  for (const child of children) if (child.exitCode === null) child.kill('SIGTERM');
  await server.stop();
}
