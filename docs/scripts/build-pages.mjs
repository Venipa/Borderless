import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';

process.env.GITHUB_PAGES = 'true';
// next.config derives basePath from NEXT_PUBLIC_URL (CI) or local fallback

const nextBin = join(process.cwd(), 'node_modules', 'next', 'dist', 'bin', 'next');
const build = spawnSync(process.execPath, [nextBin, 'build'], {
  stdio: 'inherit',
  env: process.env,
});

if (build.status !== 0) {
  process.exit(build.status ?? 1);
}

writeFileSync(join(process.cwd(), 'out', '.nojekyll'), '');
