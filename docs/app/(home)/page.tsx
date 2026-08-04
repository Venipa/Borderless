import Image from 'next/image';
import Link from 'next/link';
import {
  ArrowUpRightIcon,
  BookOpenIcon,
  DownloadIcon,
  MonitorIcon,
  MousePointerClickIcon,
  Settings2Icon,
  VolumeXIcon,
} from 'lucide-react';
import { buttonVariants } from 'fumadocs-ui/components/ui/button';
import { cn } from '@/lib/cn';
import {
  formatBytes,
  formatReleaseDate,
  getDownloadLabel,
  getLatestRelease,
  getLatestReleaseUrl,
  getReleaseSummary,
  getReleasesUrl,
  listUserDownloads,
  pickPrimaryDownload,
} from '@/lib/github';
import { appName, docsRoute } from '@/lib/shared';

const highlights = [
  {
    title: 'Match windows',
    description: 'Title, executable, or regex — pick live from running processes.',
    href: `${docsRoute}/features/`,
    icon: MonitorIcon,
  },
  {
    title: 'Borderless chrome',
    description: 'Force borderless, always-on-top, expand, or custom bounds.',
    href: `${docsRoute}/usage/`,
    icon: Settings2Icon,
  },
  {
    title: 'Input & audio',
    description: 'Lock or hide the cursor, strip menus, mute background audio.',
    href: `${docsRoute}/features/`,
    icon: VolumeXIcon,
  },
  {
    title: 'Quick install',
    description: 'Setup, portable zip, or build from source on Windows.',
    href: `${docsRoute}/install/`,
    icon: MousePointerClickIcon,
  },
] as const;

export default async function HomePage() {
  const release = await getLatestRelease();
  const primaryAsset = release ? pickPrimaryDownload(release.assets) : undefined;
  const summary = release ? getReleaseSummary(release.body) : null;
  const downloadUrl = primaryAsset?.browser_download_url ?? getLatestReleaseUrl();

  return (
    <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-16 px-4 py-12 md:gap-20 md:py-16">
      <section className="relative overflow-hidden rounded-2xl border bg-fd-card">
        <div aria-hidden className="pointer-events-none absolute inset-0 z-0 overflow-hidden">
          <div className="absolute -bottom-[8%] -left-[10%] w-[85%] max-w-3xl [perspective:1600px] md:-bottom-[6%] md:-left-[4%] md:w-[72%]">
            <div className="origin-center opacity-55 shadow-2xl shadow-black/30 [transform:rotateX(14deg)_rotateY(22deg)_rotateZ(-3deg)_scale(1.08)] [transform-style:preserve-3d] dark:opacity-50">
              <Image
                src="/app-screenshot-1.png"
                alt=""
                width={1018}
                height={673}
                className="h-auto w-full rounded-xl border border-white/10"
                sizes="(max-width: 768px) 90vw, 720px"
                priority
              />
            </div>
          </div>
          <div className="absolute inset-0 bg-gradient-to-r from-transparent via-fd-card/40 to-fd-card" />
          <div className="absolute inset-0 bg-gradient-to-t from-fd-card/85 via-transparent to-fd-card/55" />
        </div>

        <div className="relative z-10 grid items-start gap-10 px-6 py-12 md:px-12 md:py-16 lg:grid-cols-[1.15fr_0.85fr]">
          <div className="flex flex-col items-start text-left">
            <div className="mb-6 inline-flex items-center gap-3">
              <Image
                src="/logo.png"
                alt=""
                width={56}
                height={56}
                className="rounded-xl"
                priority
              />
              <span className="text-sm font-medium tracking-wide text-fd-muted-foreground uppercase">
                {appName}
              </span>
            </div>
            <h1 className="max-w-2xl text-4xl font-semibold tracking-tight text-balance md:text-5xl">
              Keep Windows games and apps borderless
            </h1>
            <p className="mt-4 max-w-xl text-base text-fd-muted-foreground text-pretty md:text-lg">
              Match by title and executable, then re-apply window styles while {appName} runs —
              tray-friendly, rule-based, built for Windows.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                href={docsRoute}
                className={cn(buttonVariants({ variant: 'primary' }), 'gap-2 px-4 py-2')}
              >
                <BookOpenIcon className="size-4" />
                Read the docs
              </Link>
              <a
                href={downloadUrl}
                className={cn(buttonVariants({ variant: 'outline' }), 'gap-2 px-4 py-2')}
                {...(primaryAsset ? {} : { target: '_blank', rel: 'noreferrer' })}
              >
                <DownloadIcon className="size-4" />
                {primaryAsset ? `Download ${release?.tag_name}` : 'Download latest'}
              </a>
            </div>
          </div>

          <aside className="relative rounded-2xl border bg-fd-background/85 p-6 backdrop-blur-md">
            <div className="mb-4 flex items-center justify-between gap-3">
              <p className="text-sm font-medium text-fd-muted-foreground">Latest release</p>
              {release ? (
                <span className="rounded-full border bg-fd-secondary px-2.5 py-1 text-xs font-medium">
                  {release.tag_name}
                </span>
              ) : null}
            </div>

            {release ? (
              <>
                <h2 className="text-xl font-semibold tracking-tight">{release.name}</h2>
                <p className="mt-1 text-sm text-fd-muted-foreground">
                  Published {formatReleaseDate(release.published_at)}
                </p>
                {summary ? (
                  <p className="mt-4 text-sm text-fd-muted-foreground text-pretty">{summary}</p>
                ) : null}

                <ul className="mt-5 space-y-2">
                  {listUserDownloads(release.assets).map((asset) => {
                    const label = getDownloadLabel(asset);

                    return (
                      <li key={asset.name}>
                        <a
                          href={asset.browser_download_url}
                          className="group flex items-center justify-between gap-3 rounded-xl border px-3 py-2.5 text-sm transition-colors hover:bg-fd-accent"
                        >
                          <span className="min-w-0">
                            <span className="block font-medium">{label.title}</span>
                            <span className="block truncate text-xs text-fd-muted-foreground">
                              {label.description}
                            </span>
                          </span>
                          <span className="shrink-0 text-xs text-fd-muted-foreground">
                            {formatBytes(asset.size)}
                          </span>
                        </a>
                      </li>
                    );
                  })}
                </ul>

                <a
                  href={release.html_url}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-5 inline-flex items-center gap-1.5 text-sm font-medium text-fd-primary hover:underline"
                >
                  View on GitHub
                  <ArrowUpRightIcon className="size-3.5" />
                </a>
              </>
            ) : (
              <>
                <h2 className="text-xl font-semibold tracking-tight">Release feed unavailable</h2>
                <p className="mt-2 text-fd-muted-foreground text-sm">
                  Open GitHub Releases for the newest installer and portable builds.
                </p>
                <a
                  href={getReleasesUrl()}
                  target="_blank"
                  rel="noreferrer"
                  className={cn(buttonVariants({ variant: 'secondary' }), 'mt-5 gap-2 px-4 py-2')}
                >
                  Browse releases
                  <ArrowUpRightIcon className="size-4" />
                </a>
              </>
            )}
          </aside>
        </div>
      </section>

      <section>
        <div className="mb-8 max-w-2xl">
          <h2 className="text-2xl font-semibold tracking-tight md:text-3xl">Start here</h2>
          <p className="mt-2 text-fd-muted-foreground text-pretty">
            Install, write rules, and explore the feature set — or build from source.
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          {highlights.map((item) => (
            <Link
              key={item.title}
              href={item.href}
              className="group rounded-2xl border bg-fd-card p-5 transition-colors hover:bg-fd-accent/60"
            >
              <item.icon className="mb-3 size-5 text-fd-primary" />
              <h3 className="font-medium tracking-tight group-hover:underline">{item.title}</h3>
              <p className="mt-1.5 text-sm text-fd-muted-foreground text-pretty">
                {item.description}
              </p>
            </Link>
          ))}
        </div>
      </section>
    </main>
  );
}
