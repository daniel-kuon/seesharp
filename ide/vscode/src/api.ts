// Client for SeeSharp's local plugin API (docs/IDE-PLUGINS.md). Plain node:http, no vscode import, so it is testable outside VS Code.
import * as fs from 'node:fs';
import * as http from 'node:http';
import * as os from 'node:os';
import * as path from 'node:path';

export interface Server { url: string; token: string; pid: number }

export interface MissingSkill { id: string; label: string; level: number | null }
export interface Exercise {
  id: string; title: string; optional: boolean; unlocked: boolean; passed: boolean;
  missingSkills: MissingSkill[];
  items: { total: number; passed: number; coding: boolean };
  starterDirectory: string | null; workspaceDirectory: string | null;
  openFile: string | null; openLine: number | null; instructionsPath: string;
}
export interface Module { id: string; level: number; title: string; unlocked: boolean; passed: boolean; exercises: Exercise[] }
export interface Course { locale: string; modules: Module[] }
export interface ExerciseDetail { exercise: Exercise; instructionsHtml: string | null; instructionsFellBack: boolean }
export interface TestResult { name: string; outcome: 'Passed' | 'Failed' | 'Skipped'; duration: string; message: string | null }
export interface GradingResult { outcome: 'Passed' | 'Failed' | 'BuildFailed' | 'TimedOut'; duration: string; tests: TestResult[]; buildErrors: string[]; output: string }
export interface Grade { passed: boolean; result: GradingResult }
export interface WorkspaceFiles { directory: string; files: string[] }

/** HTTP status 0 = not reachable. */
export class ApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message); }
}

/** Where the .NET side's Environment.SpecialFolder.LocalApplicationData points; SEESHARP_SERVER_JSON overrides (tests). */
export function serverFilePath(): string {
  if (process.env.SEESHARP_SERVER_JSON) return process.env.SEESHARP_SERVER_JSON;
  const base = process.platform === 'win32' ? process.env.LOCALAPPDATA ?? path.join(os.homedir(), 'AppData', 'Local')
    : process.platform === 'darwin' ? path.join(os.homedir(), 'Library', 'Application Support')
    : process.env.XDG_DATA_HOME ?? path.join(os.homedir(), '.local', 'share');
  return path.join(base, 'SeeSharp', 'server.json');
}

export function readServer(file = serverFilePath()): Server | undefined {
  try {
    const s = JSON.parse(fs.readFileSync(file, 'utf8'));
    return typeof s.url === 'string' && typeof s.token === 'string' ? s : undefined;
  } catch {
    return undefined;
  }
}

export class Api {
  constructor(private readonly server: Server) {}

  course(): Promise<Course> { return this.call('GET', '/api/course'); }
  /** Re-reads settings.json and the course (after `seesharp init`); also answers while no course is loaded. */
  reload(): Promise<{ loaded: boolean }> { return this.call('POST', '/api/reload'); }
  exercise(id: string): Promise<ExerciseDetail> { return this.call('GET', `/api/exercise/${encodeURIComponent(id)}`); }
  grade(id: string): Promise<Grade> { return this.call('POST', `/api/exercise/${encodeURIComponent(id)}/grade`); }
  workspace(id: string): Promise<WorkspaceFiles> { return this.call('POST', `/api/exercise/${encodeURIComponent(id)}/workspace`); }

  /** node:http sends no Origin header, which the server would reject unless it is its own. */
  private call<T>(method: string, route: string): Promise<T> {
    return new Promise((resolve, reject) => {
      const request = http.request(new URL(route, this.server.url), {
        method,
        headers: { 'X-SeeSharp-Token': this.server.token, 'Content-Length': 0 },
        timeout: 5 * 60_000, // a grade run builds and tests; GradingRunner's own timeout is 120 s
      }, response => {
        let body = '';
        response.setEncoding('utf8');
        response.on('data', chunk => body += chunk);
        response.on('end', () => {
          let json: any;
          try { json = JSON.parse(body); } catch { json = undefined; }
          const status = response.statusCode ?? 0;
          if (status >= 200 && status < 300 && json !== undefined) resolve(json);
          else reject(new ApiError(status, json?.error ?? `HTTP ${status}`));
        });
      });
      request.on('timeout', () => request.destroy(new Error('timeout')));
      request.on('error', e => reject(new ApiError(0, e.message)));
      request.end();
    });
  }
}
