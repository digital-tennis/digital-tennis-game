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
const codeFile = path.join(out, 'room-code.txt');
await fs.rm(codeFile, { force: true });
for (const role of ['host', 'guest']) {
  await fs.rm(path.join(out, `${role}.json`), { force: true });
  await fs.rm(path.join(out, `${role}-view.png`), { force: true });
}
const { startServer } = await import(pathToFileURL(path.join(serverRepo, 'src/server.js')));
const server = await startServer({ port: 0 });
const children = [];
let reconnected = false;
const exerciseReconnect = args.includes('--exercise-reconnect');
const monitor = setInterval(() => {
  const room = [...server.rooms.rooms.values()][0];
  console.log(`Online progress: ${room?.state ?? 'waiting'} epoch=${room?.epoch ?? 0} relay=${server.rooms.relayCount}`);
  if (exerciseReconnect && !reconnected && room?.state === 'PLAYING' && server.rooms.relayCount >= 180) {
    reconnected = true; room.players.guest.client.terminate();
    console.log('Verification: interrupted guest socket once; waiting for token resume and epoch 2');
  }
}, 10000);
try {
  const launch = role => {
    const child = spawn(role === 'host' ? hostPlayer : guestPlayer, ['-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '800',
      '-dt-online-role', role, '-dt-server', server.url, '-dt-code-file', codeFile,
      '-dt-report', path.join(out, `${role}.json`), '-dt-view-image', path.join(out, `${role}-view.png`), '-dt-timeout', '150', '-logFile', path.join(out, `${role}.log`)], { stdio: 'ignore' });
    children.push(child); console.log(`Started ${role} Unity PID ${child.pid}`);
    return new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', code => resolve({ role, code, pid: child.pid })); });
  };
  const hostDone = launch('host'), guestDone = launch('guest');
  const results = await Promise.all([hostDone, guestDone]);
  const host = JSON.parse(await fs.readFile(path.join(out, 'host.json'))), guest = JSON.parse(await fs.readFile(path.join(out, 'guest.json')));
  await fs.writeFile(path.join(out, 'combined.json'), JSON.stringify({ processes: results, relayedMessages: server.rooms.relayCount, host, guest }, null, 2));
  for (const result of results) assert.equal(result.code, 0, `${result.role} validation failed; inspect its log/report`);
  assert.notEqual(results[0].pid, results[1].pid);
  await fs.access(path.join(out, 'host-view.png')); await fs.access(path.join(out, 'guest-view.png'));
  assert.deepEqual(host.points, guest.points); assert.equal(host.winner, guest.winner);
  assert.equal(host.epoch, guest.epoch); assert.equal(guest.physicsTicks, 0); assert.ok(host.physicsTicks > 0);
  assert.ok(server.rooms.relayCount > 0);
  assert.equal(host.view.localPlayer, 'Near'); assert.equal(guest.view.localPlayer, 'Far');
  assert.ok(host.view.cameraPosition[2] < 0 && guest.view.cameraPosition[2] > 0);
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
  console.log(`PASS: two Unity processes, ${host.points.join('-')}, winner=${host.winner}, rally=${host.maxRallyHits}, relay=${server.rooms.relayCount}`);
  console.log(`Reports: ${out}`);
} finally {
  clearInterval(monitor);
  for (const child of children) if (child.exitCode === null) child.kill('SIGTERM');
  await server.stop();
}
