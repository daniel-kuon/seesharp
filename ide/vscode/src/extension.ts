import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import { Api, ApiError, Course, Exercise, ExerciseDetail, Grade, Module, readServer } from './api';
import { defaultCourseDir, findTool, hasSdk, initArgs, run, SDK_DOWNLOAD, startArgs, toolArgs } from './setup';

const t = vscode.l10n.t;
/** Exercise to continue with after vscode.openFolder reloaded the window. */
const PENDING = 'seesharp.pendingExercise';

let context: vscode.ExtensionContext;
let api: Api | undefined;
let serverUrl = '';
let course: Course | undefined;
let view: vscode.TreeView<Module | Exercise>;
let current: Exercise | undefined;
let panel: vscode.WebviewPanel | undefined;
let detail: ExerciseDetail | undefined;
let lastGrade: Grade | undefined;
let running = false;
let output: vscode.OutputChannel | undefined;

class Tree implements vscode.TreeDataProvider<Module | Exercise> {
  private readonly changed = new vscode.EventEmitter<void>();
  readonly onDidChangeTreeData = this.changed.event;
  refresh() { this.changed.fire(); }

  getChildren(node?: Module | Exercise): (Module | Exercise)[] {
    if (!course) return [];
    if (!node) return course.modules;
    return 'exercises' in node ? node.exercises : [];
  }

  getTreeItem(node: Module | Exercise): vscode.TreeItem {
    if ('exercises' in node) {
      const next = course!.modules.find(m => m.unlocked && !m.passed);
      const item = new vscode.TreeItem(`${String(node.level).padStart(2, '0')} ${node.title}`,
        node === next ? vscode.TreeItemCollapsibleState.Expanded : vscode.TreeItemCollapsibleState.Collapsed);
      item.iconPath = node.passed ? passedIcon() : new vscode.ThemeIcon(node.unlocked ? 'book' : 'lock');
      return item;
    }
    const item = new vscode.TreeItem(node.title);
    item.description = node.optional ? t('optional') : undefined;
    item.iconPath = !node.unlocked ? new vscode.ThemeIcon('lock') : node.passed ? passedIcon() : new vscode.ThemeIcon('circle-large-outline');
    const tip = new vscode.MarkdownString(`**${escapeMarkdown(node.title)}**\n\n`);
    tip.appendText(t('{0} of {1} tasks done', node.items.passed, node.items.total));
    if (!node.unlocked) {
      tip.appendMarkdown(`\n\n${escapeMarkdown(t('Missing skills:'))}\n`);
      for (const s of node.missingSkills)
        tip.appendMarkdown(`\n- ${escapeMarkdown(s.level === null ? s.label : t('{0} (level {1})', s.label, s.level))}`);
    }
    item.tooltip = tip;
    item.command = { command: 'seesharp.openExercise', title: t('Open exercise'), arguments: [node.id] };
    return item;
  }
}

const passedIcon = () => new vscode.ThemeIcon('pass-filled', new vscode.ThemeColor('testing.iconPassed'));
const escapeMarkdown = (s: string) => s.replace(/[\\`*_{}[\]()#+\-.!|<>]/g, '\\$&');
const escapeHtml = (s: string) => s.replace(/[&<>"']/g, c => `&#${c.charCodeAt(0)};`);
const samePath = (a: string, b: string) => process.platform === 'win32'
  ? path.resolve(a).toLowerCase() === path.resolve(b).toLowerCase() : path.resolve(a) === path.resolve(b);
const isOpenFolder = (dir: string) => vscode.workspace.workspaceFolders?.some(f => samePath(f.uri.fsPath, dir)) ?? false;

export async function activate(ctx: vscode.ExtensionContext) {
  context = ctx;
  const tree = new Tree();
  view = vscode.window.createTreeView('seesharp.exercises', { treeDataProvider: tree });
  ctx.subscriptions.push(view,
    vscode.commands.registerCommand('seesharp.refresh', () => load(tree)),
    vscode.commands.registerCommand('seesharp.start', () => start(tree)),
    vscode.commands.registerCommand('seesharp.setup', () => setup(tree)),
    vscode.commands.registerCommand('seesharp.openExercise', (id: string) => openExercise(id)),
    vscode.commands.registerCommand('seesharp.grade', () => grade(tree)));

  if (!await load(tree)) return;
  // Reopened in an exercise's folder: "Prüfen" works right away; continue an exercise picked before the window reloaded.
  const exercises = course!.modules.flatMap(m => m.exercises);
  setCurrent(exercises.find(e => e.workspaceDirectory && isOpenFolder(e.workspaceDirectory)));
  const pending = ctx.globalState.get<string>(PENDING);
  const resume = exercises.find(e => e.id === pending && e.workspaceDirectory && isOpenFolder(e.workspaceDirectory));
  if (resume) {
    await ctx.globalState.update(PENDING, undefined);
    await openExercise(resume.id);
  }
}

export function deactivate() {}

/** Reads server.json and the course; false when SeeSharp is not reachable or refused. */
async function load(tree: Tree): Promise<boolean> {
  const server = readServer();
  api = server && new Api(server);
  serverUrl = server?.url ?? '';
  course = undefined;
  view.message = undefined;
  let noCourse = false;
  if (api)
    try {
      course = await api.course();
    } catch (e) {
      noCourse = e instanceof ApiError && e.status === 503; // the welcome view then offers "Set up SeeSharp"
      if (e instanceof ApiError && e.status !== 0) view.message = noCourse ? undefined : e.message; // exam running, ...
      else api = undefined;
    }
  await vscode.commands.executeCommand('setContext', 'seesharp.offline', !api);
  await vscode.commands.executeCommand('setContext', 'seesharp.noCourse', noCourse);
  tree.refresh();
  return course !== undefined;
}

function setCurrent(exercise: Exercise | undefined) {
  current = exercise;
  void vscode.commands.executeCommand('setContext', 'seesharp.hasExercise', !!(exercise?.unlocked && exercise.items.coding));
}

/** `seesharp start --no-browser`, or `dotnet run` on a checkout; no shell in between, so paths need no quoting. */
async function start(tree: Tree) {
  const cli = vscode.workspace.getConfiguration('seesharp').get<string>('cliPath')?.trim();
  const project = cli && fs.existsSync(path.join(cli, 'src', 'SeeSharp.Cli')) ? path.join(cli, 'src', 'SeeSharp.Cli') : cli;
  const terminal = project
    ? vscode.window.createTerminal({ name: 'SeeSharp', shellPath: 'dotnet', shellArgs: ['run', '--project', project, '--', 'start', '--no-browser'] })
    : vscode.window.createTerminal({ name: 'SeeSharp', shellPath: findTool() ?? 'seesharp', shellArgs: startArgs() });
  terminal.show(true);
  const started = await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: t('Starting SeeSharp …') }, async () => {
    for (let i = 0; i < 120; i++) { // dotnet run builds first
      await new Promise(r => setTimeout(r, 1000));
      if (await load(tree)) return true;
    }
    return false;
  });
  if (!started) void vscode.window.showErrorMessage(t('SeeSharp did not start. See the "SeeSharp" terminal.'));
}

/**
 * "Set up SeeSharp": .NET 10 SDK check, `dotnet tool install|update -g SeeSharp.Cli`, `seesharp init --dir <folder>`,
 * then reload a running server or start one. Each step runs without a shell; its output goes to the "SeeSharp" channel.
 */
async function setup(tree: Tree) {
  const out = output ??= vscode.window.createOutputChannel('SeeSharp');
  out.show(true);
  const step = async (command: string, args: string[]) => {
    out.appendLine(`> ${command} ${args.map(a => /\s/.test(a) ? `"${a}"` : a).join(' ')}`);
    try {
      const code = await run(command, args, s => out.append(s));
      out.appendLine(`(exit ${code})`);
      return code === 0;
    } catch (e) {
      out.appendLine(e instanceof Error ? e.message : String(e));
      return false;
    }
  };

  let sdks = '';
  await run('dotnet', ['--list-sdks'], s => sdks += s).catch(() => -1); // no dotnet at all = no SDK
  out.appendLine('> dotnet --list-sdks');
  out.append(sdks);
  if (!hasSdk(sdks)) {
    const download = t('Download .NET 10');
    if (await vscode.window.showErrorMessage(t('SeeSharp needs the .NET 10 SDK. Install it, restart VS Code and run "Set up SeeSharp" again.'), download) === download)
      void vscode.env.openExternal(vscode.Uri.parse(SDK_DOWNLOAD));
    return;
  }

  let tool = findTool();
  let install = !tool;
  if (tool) {
    const update = t('Update'), keep = t('Keep version');
    const choice = await vscode.window.showInformationMessage(t('SeeSharp is already installed. Update it to the latest version?'), { modal: true }, update, keep);
    if (!choice) return;
    install = choice === update;
  }
  const folder = await vscode.window.showInputBox({
    title: t('Set up SeeSharp'), prompt: t('Course folder: the course is cloned there, or updated if it is already a clone.'),
    value: defaultCourseDir(), ignoreFocusOut: true,
  });
  if (!folder?.trim()) return;

  const ok = await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: t('Setting up SeeSharp') }, async progress => {
    if (install) {
      progress.report({ message: tool ? t('Updating the seesharp tool …') : t('Installing the seesharp tool …') });
      const source = vscode.workspace.getConfiguration('seesharp').get<string>('extraPackageSource');
      // An update fails while SeeSharp runs (the .exe is in use); the installed version then carries on.
      if (!await step('dotnet', toolArgs(!!tool, source)) && !tool) {
        void vscode.window.showErrorMessage(t('Installing SeeSharp failed. See the "SeeSharp" output.'));
        return false;
      }
      tool = findTool();
    }
    if (!tool) {
      void vscode.window.showErrorMessage(t('The seesharp tool was not found after installing it. See the "SeeSharp" output.'));
      return false;
    }
    progress.report({ message: t('Cloning the course and downloading the book …') });
    if (!await step(tool, initArgs(folder.trim()))) {
      void vscode.window.showErrorMessage(t('"seesharp init" failed. See the "SeeSharp" output.'));
      return false;
    }
    return true;
  });
  if (!ok) return;

  await load(tree);
  if (api) { // already running: let it re-read the course path from settings.json
    await api.reload().catch(report);
    await load(tree);
    void vscode.window.showInformationMessage(t('SeeSharp is set up.'));
  } else
    await start(tree);
}

async function openExercise(id: string) {
  if (!api) return offline();
  try {
    detail = await api.exercise(id);
    lastGrade = undefined;
    const ex = detail.exercise;
    setCurrent(ex);
    if (ex.unlocked && ex.items.coding && ex.workspaceDirectory && ex.openFile) {
      await api.workspace(id);
      if (!isOpenFolder(ex.workspaceDirectory)) {
        const here = t('Open folder'), beside = t('New window');
        const choice = await vscode.window.showInformationMessage(
          t('Open the project folder of "{0}" in VS Code?', ex.title), here, beside);
        if (choice) {
          // The window that opens the folder resumes in activate(): file, line and instructions.
          await context.globalState.update(PENDING, id);
          await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(ex.workspaceDirectory), { forceNewWindow: choice === beside });
          return;
        }
      }
      const editor = await vscode.window.showTextDocument(vscode.Uri.file(ex.openFile), { viewColumn: vscode.ViewColumn.One, preview: false });
      if (ex.openLine) {
        const at = new vscode.Position(ex.openLine - 1, 0);
        editor.selection = new vscode.Selection(at, at);
        editor.revealRange(new vscode.Range(at, at), vscode.TextEditorRevealType.InCenter);
      }
    }
    showPanel();
  } catch (e) {
    report(e);
  }
}

async function grade(tree: Tree) {
  const ex = current;
  if (!api) return offline();
  if (!ex) return;
  await vscode.workspace.saveAll(false); // grading copies the files from disk
  running = true;
  if (detail?.exercise.id === ex.id) showPanel();
  try {
    const client = api;
    lastGrade = await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: t('Running the tests of "{0}" …', ex.title) },
      () => client.grade(ex.id));
    const r = lastGrade.result;
    const summary = r.tests.length ? t('{0} of {1} tests passed.', r.tests.filter(x => x.outcome === 'Passed').length, r.tests.length) : outcome(r.outcome);
    if (lastGrade.passed) void vscode.window.showInformationMessage(t('Passed! {0}', summary));
    else void vscode.window.showWarningMessage(t('Not passed yet. {0}', summary));
    await load(tree);
  } catch (e) {
    report(e);
  } finally {
    running = false;
    if (detail?.exercise.id === ex.id) showPanel();
  }
}

function outcome(o: string): string {
  switch (o) {
    case 'Passed': return t('Passed');
    case 'Failed': return t('Failed');
    case 'BuildFailed': return t('Build failed');
    default: return t('Timed out');
  }
}

function offline() {
  const start = t('Start SeeSharp');
  void vscode.window.showWarningMessage(t('SeeSharp is not running.'), start)
    .then(c => c === start && vscode.commands.executeCommand('seesharp.start'));
}

function report(e: unknown) {
  if (e instanceof ApiError && e.status === 0) return offline();
  void vscode.window.showErrorMessage(e instanceof Error ? e.message : String(e));
}

function showPanel() {
  if (!detail) return;
  if (!panel) {
    panel = vscode.window.createWebviewPanel('seesharp.exercise', detail.exercise.title,
      { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true }, { enableScripts: true });
    panel.onDidDispose(() => panel = undefined);
    panel.webview.onDidReceiveMessage(m => m?.command === 'grade' && vscode.commands.executeCommand('seesharp.grade'));
  }
  panel.title = detail.exercise.title;
  panel.webview.html = html(panel.webview, detail);
}

function html(webview: vscode.Webview, d: ExerciseDetail): string {
  const nonce = Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join('');
  const ex = d.exercise;
  let body: string;
  if (!ex.unlocked)
    body = `<p>${escapeHtml(t('This exercise is still locked. Missing skills:'))}</p><ul>${
      ex.missingSkills.map(s => `<li>${escapeHtml(s.level === null ? s.label : t('{0} (level {1})', s.label, s.level))}</li>`).join('')}</ul>`;
  else {
    const top = ex.items.coding
      ? `<button id="grade" ${running ? 'disabled' : ''}>${escapeHtml(running ? t('Tests are running …') : t('Check'))}</button>${results()}`
      : `<p>${escapeHtml(t('Answer the questions of this exercise in SeeSharp:'))} <a href="${escapeHtml(`${serverUrl}/exercise/${encodeURIComponent(ex.id)}`)}">${escapeHtml(t('open in the browser'))}</a></p>`;
    // Course content is trusted (the trainee's own checkout) and rendered as is; the CSP still blocks any script in it.
    body = `${top}<hr>${d.instructionsFellBack ? `<p class="note">${escapeHtml(t('No translation yet, showing German.'))}</p>` : ''}${d.instructionsHtml ?? ''}`;
  }
  return `<!DOCTYPE html><html><head><meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src ${webview.cspSource} https: data:; script-src 'nonce-${nonce}';">
<style>
body { font-family: var(--vscode-font-family); font-size: var(--vscode-font-size); color: var(--vscode-foreground); line-height: 1.5; }
button { background: var(--vscode-button-background); color: var(--vscode-button-foreground); border: 0; padding: 4px 14px; cursor: pointer; }
button:disabled { opacity: .6; cursor: default; }
.ok { color: var(--vscode-testing-iconPassed); } .bad { color: var(--vscode-testing-iconFailed); } .note { opacity: .8; }
pre, code { font-family: var(--vscode-editor-font-family); } pre { background: var(--vscode-textCodeBlock-background); padding: 6px; overflow-x: auto; }
table { border-collapse: collapse; } td { padding: 2px 8px; vertical-align: top; border-bottom: 1px solid var(--vscode-panel-border); }
</style></head><body><h1>${escapeHtml(ex.title)}</h1>${body}
<script nonce="${nonce}">const vscode = acquireVsCodeApi(); document.getElementById('grade')?.addEventListener('click', () => vscode.postMessage({ command: 'grade' }));</script>
</body></html>`;
}

/** The last run of this exercise in this session; test names and messages are escaped (they come from the trainee's code). */
function results(): string {
  if (!lastGrade || detail?.exercise.id !== current?.id) return '';
  const r = lastGrade.result;
  let html = `<p class="${lastGrade.passed ? 'ok' : 'bad'}"><strong>${escapeHtml(lastGrade.passed ? t('Passed') : t('Not passed yet'))}</strong> · ${
    escapeHtml(outcome(r.outcome))} · ${escapeHtml(r.duration.replace(/\.\d+$/, ''))}</p>`;
  if (r.buildErrors.length) html += `<pre>${escapeHtml(r.buildErrors.join('\n'))}</pre>`;
  else if (r.tests.length) html += `<table>${r.tests.map(x => `<tr><td class="${x.outcome === 'Passed' ? 'ok' : x.outcome === 'Failed' ? 'bad' : ''}">${
    escapeHtml(x.outcome === 'Passed' ? '✓' : x.outcome === 'Failed' ? '✗' : '–')}</td><td><code>${escapeHtml(x.name)}</code>${
    x.message ? `<pre>${escapeHtml(x.message)}</pre>` : ''}</td></tr>`).join('')}</table>`;
  else html += `<pre>${escapeHtml(r.output.slice(-3000))}</pre>`;
  return html;
}
