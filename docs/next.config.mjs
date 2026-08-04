import { createMDX } from 'fumadocs-mdx/next';

const withMDX = createMDX();

/**
 * Derive Next.js basePath from a site URL (e.g. GitHub Pages page_url / base_url).
 * https://owner.github.io/Borderless/ → /Borderless
 * https://owner.github.io/ → ""
 */
function basePathFromSiteUrl(url) {
  if (!url) return '';
  try {
    return new URL(url).pathname.replace(/\/+$/, '');
  } catch {
    return '';
  }
}

const isGithubPages = process.env.GITHUB_PAGES === 'true';
if (isGithubPages && !process.env.NEXT_PUBLIC_URL) {
  process.env.NEXT_PUBLIC_URL = 'https://local.pages/Borderless';
}

const siteUrl = process.env.NEXT_PUBLIC_URL ?? '';
const basePath = basePathFromSiteUrl(siteUrl);

/** @type {import('next').NextConfig} */
const config = {
  output: 'export',
  reactStrictMode: true,
  trailingSlash: true,
  images: {
    unoptimized: true,
  },
  ...(basePath
    ? {
        basePath,
        assetPrefix: `${basePath}/`,
      }
    : {}),
  env: {
    NEXT_PUBLIC_URL: siteUrl,
  },
};

export default withMDX(config);
