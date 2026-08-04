import { GitHubIcon, XIcon } from '@/components/icons';
import { getRepositoryUrl } from '@/lib/github';
import { appName, socials } from '@/lib/shared';

export function Footer() {
  const year = new Date().getFullYear();

  return (
    <footer className="mt-auto border-t border-fd-border/60">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 px-4 py-6 text-sm text-fd-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <p>
          © {year} {socials.github.handle} · {appName} ·{' '}
          <a
            href={`${getRepositoryUrl()}/blob/master/LICENSE`}
            target="_blank"
            rel="noreferrer"
            className="underline-offset-4 hover:text-fd-foreground hover:underline"
          >
            GPL-3.0
          </a>
        </p>
        <div className="flex items-center gap-3">
          <a
            href={socials.x.url}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 hover:text-fd-foreground"
            aria-label={`X ${socials.x.handle}`}
          >
            <XIcon className="size-3.5" />
            <span>{socials.x.handle}</span>
          </a>
          <span aria-hidden className="text-fd-border">
            ·
          </span>
          <a
            href={socials.github.url}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 hover:text-fd-foreground"
            aria-label={`GitHub ${socials.github.handle}`}
          >
            <GitHubIcon className="size-3.5" />
            <span>{socials.github.handle}</span>
          </a>
        </div>
      </div>
    </footer>
  );
}
