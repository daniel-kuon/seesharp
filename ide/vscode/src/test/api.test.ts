// `npm test`. The API tests need a running SeeSharp and SEESHARP_SERVER_JSON pointing at its server.json
// (a scratch host on a copy of the course: grading and the workspace write into the course folder).
import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { test } from 'node:test';
import { Api, ApiError, readServer } from '../api';

const root = path.join(__dirname, '..', '..');

test('every l10n string has a German translation, and both nls files have the same keys', () => {
  const source = fs.readFileSync(path.join(root, 'src', 'extension.ts'), 'utf8');
  const keys = [...source.matchAll(/\bt\('([^']*)'/g)].map(m => m[1]);
  const de = JSON.parse(fs.readFileSync(path.join(root, 'l10n', 'bundle.l10n.de.json'), 'utf8'));
  assert.deepEqual(keys.filter(k => !(k in de)), []);
  const nls = (f: string) => Object.keys(JSON.parse(fs.readFileSync(path.join(root, f), 'utf8'))).sort();
  assert.deepEqual(nls('package.nls.de.json'), nls('package.nls.json'));
});

const server = process.env.SEESHARP_SERVER_JSON ? readServer() : undefined;

test('API against a running SeeSharp', { skip: !server && 'set SEESHARP_SERVER_JSON' }, async t => {
  const api = new Api(server!);

  await t.test('a wrong token is refused', async () => {
    await assert.rejects(new Api({ ...server!, token: 'nope' }).course(), (e: ApiError) => e.status === 403);
  });

  const course = await api.course();
  const exercises = course.modules.flatMap(m => m.exercises);
  const coding = exercises.find(e => e.unlocked && e.items.coding)!;

  await t.test('course lists modules with exercises and absolute paths', () => {
    assert.ok(course.modules.length > 0);
    assert.ok(coding, 'an unlocked coding exercise');
    assert.ok(path.isAbsolute(coding.openFile!) && fs.existsSync(coding.openFile!));
    assert.ok(coding.workspaceDirectory && coding.openFile!.startsWith(coding.workspaceDirectory));
    const locked = exercises.find(e => !e.unlocked);
    if (locked) assert.ok(locked.missingSkills.length > 0);
  });

  await t.test('exercise carries the rendered instructions', async () => {
    const detail = await api.exercise(coding.id);
    assert.equal(detail.exercise.id, coding.id);
    assert.match(detail.instructionsHtml ?? '', /<h1/);
    await assert.rejects(api.exercise('does-not-exist'), (e: ApiError) => e.status === 404);
  });

  await t.test('workspace writes the editor and assistant files', async () => {
    const ws = await api.workspace(coding.id);
    assert.ok(ws.files.includes('.vscode/tasks.json') && ws.files.includes('AGENTS.md'));
    assert.match(fs.readFileSync(path.join(ws.directory, 'AGENTS.md'), 'utf8'), /Never write the solution code/);
  });

  await t.test('grade returns the run', async () => {
    const grade = await api.grade(coding.id);
    assert.equal(typeof grade.passed, 'boolean');
    assert.ok(['Passed', 'Failed', 'BuildFailed', 'TimedOut'].includes(grade.result.outcome));
  });

  await t.test('an unreachable server is status 0', async () => {
    await assert.rejects(new Api({ ...server!, url: 'http://127.0.0.1:1' }).course(), (e: ApiError) => e.status === 0);
  });
});
