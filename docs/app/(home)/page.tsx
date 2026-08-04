import Link from 'next/link';

export default function HomePage() {
  return (
    <div className="flex flex-col justify-center text-center flex-1 px-4">
      <h1 className="text-4xl font-bold mb-3 text-balance">Borderless</h1>
      <p className="text-fd-muted-foreground max-w-xl mx-auto mb-8 text-pretty">
        Windows desktop app that keeps games and other windows in borderless layouts. Match by
        title and/or executable, then re-apply styles while Borderless runs.
      </p>
      <div className="flex flex-wrap gap-3 justify-center">
        <Link
          href="/docs"
          className="inline-flex items-center rounded-full bg-fd-primary px-5 py-2.5 text-sm font-medium text-fd-primary-foreground"
        >
          Read the docs
        </Link>
        <a
          href="https://github.com/Venipa/Borderless/releases/latest"
          className="inline-flex items-center rounded-full border px-5 py-2.5 text-sm font-medium"
        >
          Download
        </a>
      </div>
    </div>
  );
}
