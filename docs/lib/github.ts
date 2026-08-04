import { gitConfig } from './shared';

export interface ReleaseAsset {
  name: string;
  browser_download_url: string;
  size: number;
  content_type: string;
}

export interface LatestRelease {
  tag_name: string;
  name: string;
  html_url: string;
  published_at: string;
  body: string | null;
  assets: ReleaseAsset[];
}

interface GitHubReleaseResponse {
  tag_name: string;
  name: string | null;
  html_url: string;
  published_at: string;
  body: string | null;
  assets: Array<{
    name: string;
    browser_download_url: string;
    size: number;
    content_type: string;
  }>;
}

export function getRepositoryUrl(): string {
  return `https://github.com/${gitConfig.user}/${gitConfig.repo}`;
}

export function getReleasesUrl(): string {
  return `${getRepositoryUrl()}/releases`;
}

export function getLatestReleaseUrl(): string {
  return `${getRepositoryUrl()}/releases/latest`;
}

export async function getLatestRelease(): Promise<LatestRelease | null> {
  try {
    const response = await fetch(
      `https://api.github.com/repos/${gitConfig.user}/${gitConfig.repo}/releases/latest`,
      {
        headers: {
          Accept: 'application/vnd.github+json',
          'User-Agent': 'Borderless-Docs',
        },
        next: { revalidate: 3600 },
      },
    );

    if (!response.ok) {
      return null;
    }

    const data = (await response.json()) as GitHubReleaseResponse;

    return {
      tag_name: data.tag_name,
      name: data.name ?? data.tag_name,
      html_url: data.html_url,
      published_at: data.published_at,
      body: data.body,
      assets: data.assets.map((asset) => ({
        name: asset.name,
        browser_download_url: asset.browser_download_url,
        size: asset.size,
        content_type: asset.content_type,
      })),
    };
  } catch {
    return null;
  }
}

export function pickPrimaryDownload(assets: ReleaseAsset[]): ReleaseAsset | undefined {
  return assets.find((asset) => asset.name.endsWith('-setup.exe'));
}

export type DownloadKind = 'installer' | 'portable-bundled' | 'portable' | 'other';

export interface DownloadLabel {
  kind: DownloadKind;
  title: string;
  description: string;
}

export function getDownloadLabel(asset: ReleaseAsset): DownloadLabel {
  const name = asset.name.toLowerCase();

  if (name.endsWith('-setup.exe')) {
    return {
      kind: 'installer',
      title: 'Installer',
      description: 'Recommended setup (.exe)',
    };
  }

  if (name.includes('bundled') && name.endsWith('.zip')) {
    return {
      kind: 'portable-bundled',
      title: 'Portable (bundled)',
      description: '.NET runtime included',
    };
  }

  if (name.endsWith('.zip')) {
    return {
      kind: 'portable',
      title: 'Portable',
      description: 'Requires .NET runtime',
    };
  }

  return {
    kind: 'other',
    title: asset.name,
    description: 'Download',
  };
}

export function listUserDownloads(assets: ReleaseAsset[]): ReleaseAsset[] {
  const order: DownloadKind[] = ['installer', 'portable-bundled', 'portable'];

  return assets
    .filter((asset) => {
      const kind = getDownloadLabel(asset).kind;
      return kind !== 'other';
    })
    .sort(
      (left, right) =>
        order.indexOf(getDownloadLabel(left).kind) -
        order.indexOf(getDownloadLabel(right).kind),
    );
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const units = ['KB', 'MB', 'GB'] as const;
  let value = bytes / 1024;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return `${value.toFixed(value >= 10 ? 0 : 1)} ${units[unitIndex]}`;
}

export function formatReleaseDate(isoDate: string): string {
  return new Intl.DateTimeFormat('en', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(isoDate));
}

export function getReleaseNotes(body: string | null, maxItems = 6): string[] {
  if (!body) {
    return [];
  }

  return body
    .replace(/```[\s\S]*?```/g, '')
    .replace(/\r\n/g, '\n')
    .split('\n')
    .map((line) =>
      line
        .replace(/^#+\s*/, '')
        .replace(/^[-*+]\s+/, '')
        .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
        .replace(/[*_`~]/g, '')
        .trim(),
    )
    .filter((line) => line.length > 0 && !/^changes$/i.test(line))
    .slice(0, maxItems);
}
