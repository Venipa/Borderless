/** Site root when deployed under a subdirectory (e.g. GitHub Pages `/Borderless`). */
export const basePath = basePathFromSiteUrl(process.env.NEXT_PUBLIC_URL);

/**
 * Prefix a public/static asset path with the site base path.
 * Next.js `basePath` does not rewrite unoptimized `next/image` `src` values.
 */
export function assetPath(path: string): string {
  const normalized = path.startsWith('/') ? path : `/${path}`;
  return `${basePath}${normalized}`;
}

function basePathFromSiteUrl(url: string | undefined): string {
  if (!url) return '';
  try {
    return new URL(url).pathname.replace(/\/+$/, '');
  } catch {
    return '';
  }
}
