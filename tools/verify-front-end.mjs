#!/usr/bin/env node
// Exercise actual Unity menu button callbacks, normalized racket input and a full single-player game.
import {spawn} from 'node:child_process';
import {promises as fs} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const args=process.argv.slice(2);
const arg=(name,fallback)=>args.includes(name)?args[args.indexOf(name)+1]:fallback;
const out=path.resolve(arg('--out',path.join(root,'.work/front-end-proof')));
const player=path.resolve(arg('--player',path.join(root,'Builds/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis')));
await fs.access(player);await fs.mkdir(out,{recursive:true});await fs.rm(path.join(out,'result.json'),{force:true});
const balance=path.join(out,'verification-balance.csv');await fs.copyFile(path.join(root,'Assets/StreamingAssets/Balance/characters.csv'),balance);
const gameplayFile=path.join(out,'validation-gameplay.csv');
await fs.copyFile(path.join(root,'Assets/StreamingAssets/Balance/gameplay.csv'),gameplayFile);
const effects=path.join(out,'verification-effects.csv');await fs.copyFile(path.join(root,'Assets/StreamingAssets/Effects/effects.csv'),effects);
const child=spawn(player,['-batchmode','-screen-fullscreen','0','-screen-width','1600','-screen-height','900',
  '-dt-frontend-verify',out,'-dt-gameplay-file',gameplayFile,'-dt-balance-file',balance,'-dt-effects-file',effects,'-logFile',path.join(out,'frontend.log')],{stdio:'ignore'});
console.log(`Started actual Unity front end PID ${child.pid}`);
const timeout=setTimeout(()=>child.kill('SIGTERM'),210000);
try {
  const code=await new Promise((resolve,reject)=>{child.once('error',reject);child.once('exit',resolve)});
  const report=JSON.parse(await fs.readFile(path.join(out,'result.json'),'utf8'));
  assert.equal(code,0);assert.equal(report.passed,true);assert.equal(report.timedOut,false);assert.equal(report.runtimeErrors,0);
  assert.equal(report.selectableCharacters,15);assert.equal(report.pauseHeld,true);assert.equal(report.rematchReset,true);assert.ok(report.buttonActivations>=30);
  assert.ok(report.serves>=4);assert.ok(report.completedGame.nearHits>0&&report.completedGame.farHits>0);assert.ok(report.completedGame.winner);
  const stages=['Logo','Title','Menu','Options','Controls','Player','Opponent','Court','Versus','Playing','Pause','Results','Versus','Menu'];
  assert.equal(report.captures.length,stages.length);
  for(let i=0;i<stages.length;i++) {
    const name=report.captures[i];const frame=JSON.parse(await fs.readFile(path.join(out,`${name}.json`),'utf8'));
    assert.equal(frame.stage,stages[i]);assert.equal(frame.source,'running-game-cameras-with-canvas');
    if(i<9)assert.equal(frame.points,0);
    if(frame.stage!=='Playing')assert.equal(frame.gameplayEnabled,false);
    const png=await fs.readFile(path.join(out,`${name}.png`));assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
    assert.equal(png.readUInt32BE(16),1600);assert.equal(png.readUInt32BE(20),900);
  }
  console.log(`PASS: ${report.captures.length} real UI/camera frames, 15 selectable characters, ${report.serves} serves, score=${report.completedGame.points.join('-')}, winner=${report.completedGame.winner}`);
  console.log(`Reports: ${out}`);
} finally {clearTimeout(timeout);if(child.exitCode===null)child.kill('SIGTERM');}
