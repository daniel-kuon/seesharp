import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { test } from 'node:test';
import { commandText, findTool, hasSdk, initArgs, NUGET_ORG, run, sdkMajors, spawnOptions, toolArgs, userValue } from '../setup';

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
  assert.deepEqual(toolArgs(false), ['tool', 'install', '-g', 'SeeSharp.Cli', '--add-source', NUGET_ORG]);
  assert.deepEqual(toolArgs(true, ' D:\\Rider Projects 2\\SeeSharp\\nupkg '),
    ['tool', 'update', '-g', 'SeeSharp.Cli', '--add-source', NUGET_ORG, '--add-source', 'D:\\Rider Projects 2\\SeeSharp\\nupkg']);
  assert.deepEqual(toolArgs(false, '  '), ['tool', 'install', '-g', 'SeeSharp.Cli', '--add-source', NUGET_ORG]);
  assert.deepEqual(initArgs('C:\\Kurs Ü, 1'), ['init', '--dir', 'C:\\Kurs Ü, 1']);
});

test('a workspace or folder value of a setting is ignored; only the user value counts', () => {
  assert.equal(userValue({ workspaceValue: 'https://evil.example/feed', workspaceFolderValue: 'D:\\evil' }), undefined);
  assert.equal(userValue({ globalValue: ' D:\\nupkg ', workspaceValue: 'https://evil.example/feed' }), 'D:\\nupkg');
  assert.equal(userValue({ globalValue: '  ' }), undefined);
  assert.equal(userValue(undefined), undefined);
  assert.equal(commandText('dotnet', toolArgs(false, 'D:\\Rider Projects 2\\nupkg')),
    `dotnet tool install -g SeeSharp.Cli --add-source ${NUGET_ORG} --add-source "D:\\Rider Projects 2\\nupkg"`);
});

test('package.json keeps the process-starting settings out of workspace settings', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '..', 'package.json'), 'utf8'));
  for (const key of ['seesharp.cliPath', 'seesharp.extraPackageSource']) {
    assert.equal(pkg.contributes.configuration.properties[key].scope, 'machine', key);
    assert.ok(pkg.capabilities.untrustedWorkspaces.restrictedConfigurations.includes(key), key);
  }
});

test('run passes arguments without a shell and collects the output', async () => {
  let out = '';
  const code = await run(process.execPath, ['-e', 'console.log(process.argv[1])', 'Kurs Ü, "1" & $x'], s => out += s);
  assert.equal(code, 0);
  assert.equal(out.trim(), 'Kurs Ü, "1" & $x');
  await assert.rejects(run(path.join(__dirname, 'does-not-exist'), [], () => {}));
});

test('setup processes run in the home folder, never the workspace (its NuGet.config could redirect the source)', async () => {
  assert.deepEqual(spawnOptions('C:\\Users\\Jörg'), { shell: false, windowsHide: true, cwd: 'C:\\Users\\Jörg' });
  assert.equal(spawnOptions().cwd, os.homedir());
  let out = '';
  assert.equal(await run(process.execPath, ['-e', 'console.log(process.cwd())'], s => out += s), 0);
  assert.equal(path.resolve(out.trim()).toLowerCase(), path.resolve(os.homedir()).toLowerCase());
});
