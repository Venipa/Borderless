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

`build:pages` sets `NEXT_PUBLIC_BASE_PATH=/Borderless` (and Next `basePath`) and writes `out/.nojekyll`.

Public assets must use `assetPath()` from `lib/paths.ts` — unoptimized `next/image` does not rewrite `/…` under a subdirectory.

Override with env if needed:

```bash
NEXT_PUBLIC_BASE_PATH=/Borderless bun run build
```

Deployed from `.github/workflows/docs.yml` to https://venipa.github.io/Borderless/
