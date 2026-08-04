# Borderless Docs

Fumadocs site for [Borderless](https://github.com/Venipa/Borderless).

## Local

```bash
cd docs
bun install
bun run dev
```

Open http://localhost:3000

## Static build (GitHub Pages)

```bash
bun run build:pages
bun run start
```

`build:pages` derives Next `basePath` from `NEXT_PUBLIC_URL` (pathname) and writes `out/.nojekyll`.
CI passes the GitHub Pages URL; local fallback is `https://local.pages/Borderless`.

Public assets must use `assetPath()` from `lib/paths.ts` — unoptimized `next/image` does not rewrite `/…` under a subdirectory.

Override with env if needed:

```bash
NEXT_PUBLIC_URL=https://venipa.github.io/Borderless bun run build
```

Deployed from `.github/workflows/docs.yml` to https://venipa.github.io/Borderless/
