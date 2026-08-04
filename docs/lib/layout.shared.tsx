import type { BaseLayoutProps } from 'fumadocs-ui/layouts/shared';
import { BookIcon } from 'lucide-react';
import { GitHubIcon, XIcon } from '@/components/icons';
import { Logo } from '@/components/logo';
import { docsRoute, socials } from './shared';

export function baseOptions(): BaseLayoutProps {
  return {
    nav: {
      title: <Logo />,
      transparentMode: 'top',
    },
    links: [
      {
        icon: <BookIcon />,
        text: 'Documentation',
        url: docsRoute,
        active: 'nested-url',
      },
      {
        type: 'custom',
        on: 'nav',
        secondary: true,
        children: (
          <div
            role="separator"
            aria-orientation="vertical"
            className="mx-1 h-4 w-px bg-fd-border"
          />
        ),
      },
      {
        type: 'icon',
        label: `X ${socials.x.handle}`,
        icon: <XIcon className="size-4" />,
        text: 'X',
        url: socials.x.url,
        external: true,
      },
      {
        type: 'icon',
        label: `GitHub ${socials.github.handle}`,
        icon: <GitHubIcon className="size-4" />,
        text: 'GitHub',
        url: socials.github.url,
        external: true,
      },
    ],
  };
}
