// "Set up SeeSharp": the parts that need no VS Code API (SDK check, tool lookup, argument lists, running a process),
// so node:test covers them. The flow itself is in extension.ts.
import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';

export const PACKAGE_ID = 'SeeSharp.Cli';
export const REQUIRED_SDK = 10;
export const SDK_DOWNLOAD = 'https://dotnet.microsoft.com/download/dotnet/10.0';

/** Major versions from `dotnet --list-sdks`, whose lines look like `10.0.100 [C:\Program Files\dotnet\sdk]`. */
export function sdkMajors(listSdks: string): number[] {
  return listSdks.split(/\r?\n/).flatMap(line => {
    const m = /^\s*(\d+)\.\d+\.\d+/.exec(line);
    return m ? [Number(m[1])] : [];
  });
}

export const hasSdk = (listSdks: string, major = REQUIRED_SDK) => sdkMajors(listSdks).includes(major);

/** `seesharp` on PATH, else in `~/.dotnet/tools` (where `dotnet tool install -g` puts it; a running IDE's PATH may predate that). */
export function findTool(env: NodeJS.ProcessEnv = process.env, home = os.homedir(), platform: string = process.platform,
  exists: (file: string) => boolean = fs.existsSync): string | undefined {
  const p = platform === 'win32' ? path.win32 : path.posix;
  const name = platform === 'win32' ? 'seesharp.exe' : 'seesharp';
  const dirs = (env.PATH ?? env.Path ?? '').split(p.delimiter).filter(d => d.trim());
  return [...dirs, p.join(home, '.dotnet', 'tools')].map(d => p.join(d, name)).find(exists);
}

/** `dotnet tool install|update -g SeeSharp.Cli [--add-source <folder or feed>]`. */
export function toolArgs(update: boolean, extraSource?: string): string[] {
  const source = extraSource?.trim();
  return ['tool', update ? 'update' : 'install', '-g', PACKAGE_ID, ...(source ? ['--add-source', source] : [])];
}

export const initArgs = (courseDir: string) => ['init', '--dir', courseDir];
export const startArgs = () => ['start', '--no-browser'];
export const defaultCourseDir = (home = os.homedir()) => path.join(home, 'seesharp-course');

/** Runs without a shell (argument list, nothing to quote); output goes to `log` as it arrives. Rejects when the program cannot start. */
export function run(command: string, args: string[], log: (text: string) => void): Promise<number> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { shell: false, windowsHide: true });
    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', log);
    child.stderr.on('data', log);
    child.on('error', reject);
    child.on('close', code => resolve(code ?? -1));
  });
}
