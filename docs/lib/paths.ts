/** Site root when deployed under a subdirectory (e.g. GitHub Pages `/Borderless`). */
export const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? '';

/**
 * Prefix a public/static asset path with the site base path.
 * Next.js `basePath` does not rewrite unoptimized `next/image` `src` values.
 */
export function assetPath(path: string): string {
  const normalized = path.startsWith('/') ? path : `/${path}`;
  return `${basePath}${normalized}`;
}
