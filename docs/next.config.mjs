import { createMDX } from 'fumadocs-mdx/next';

const withMDX = createMDX();

const isGithubPages = process.env.GITHUB_PAGES === 'true';
const basePath =
  process.env.NEXT_PUBLIC_BASE_PATH ?? (isGithubPages ? '/Borderless' : '');

if (basePath && !process.env.NEXT_PUBLIC_BASE_PATH) {
  process.env.NEXT_PUBLIC_BASE_PATH = basePath;
}

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
    NEXT_PUBLIC_BASE_PATH: basePath,
  },
};

export default withMDX(config);
