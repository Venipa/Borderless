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

`build:pages` sets `basePath` to `/Borderless` and writes `out/.nojekyll`.

Deployed from `.github/workflows/docs.yml` to https://venipa.github.io/Borderless/
