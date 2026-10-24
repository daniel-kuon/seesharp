import * as assert from 'node:assert/strict';
import * as path from 'node:path';
import { test } from 'node:test';
import { findTool, hasSdk, initArgs, run, sdkMajors, toolArgs } from '../setup';

test('SDK detection reads the majors of dotnet --list-sdks', () => {
  const out = '8.0.404 [C:\\Program Files\\dotnet\\sdk]\r\n10.0.100 [C:\\Program Files\\dotnet\\sdk]\r\n';
  assert.deepEqual(sdkMajors(out), [8, 10]);
  assert.ok(hasSdk(out));
  assert.ok(!hasSdk('8.0.404 [/usr/share/dotnet/sdk]\n'));
  assert.ok(!hasSdk(''));
});

test('the tool is found on PATH first, then in ~/.dotnet/tools', () => {
  const home = 'C:\\Users\\Jörg Müller';
  const tools = 'C:\\Users\\Jörg Müller\\.dotnet\\tools\\seesharp.exe';
  assert.equal(findTool({ Path: 'C:\\a;C:\\b, c' }, home, 'win32', f => f === 'C:\\b, c\\seesharp.exe'), 'C:\\b, c\\seesharp.exe');
  assert.equal(findTool({ PATH: 'C:\\a' }, home, 'win32', f => f === tools), tools);
  assert.equal(findTool({ PATH: '/usr/bin' }, '/home/jörg', 'linux', f => f === '/home/jörg/.dotnet/tools/seesharp'), '/home/jörg/.dotnet/tools/seesharp');
  assert.equal(findTool({}, home, 'win32', () => false), undefined);
});

test('argument lists keep paths with spaces, umlauts and commas as one argument', () => {
  assert.deepEqual(toolArgs(false), ['tool', 'install', '-g', 'SeeSharp.Cli']);
  assert.deepEqual(toolArgs(true, ' D:\\Rider Projects 2\\SeeSharp\\nupkg '),
    ['tool', 'update', '-g', 'SeeSharp.Cli', '--add-source', 'D:\\Rider Projects 2\\SeeSharp\\nupkg']);
  assert.deepEqual(toolArgs(false, '  '), ['tool', 'install', '-g', 'SeeSharp.Cli']);
  assert.deepEqual(initArgs('C:\\Kurs Ü, 1'), ['init', '--dir', 'C:\\Kurs Ü, 1']);
});

test('run passes arguments without a shell and collects the output', async () => {
  let out = '';
  const code = await run(process.execPath, ['-e', 'console.log(process.argv[1])', 'Kurs Ü, "1" & $x'], s => out += s);
  assert.equal(code, 0);
  assert.equal(out.trim(), 'Kurs Ü, "1" & $x');
  await assert.rejects(run(path.join(__dirname, 'does-not-exist'), [], () => {}));
});
