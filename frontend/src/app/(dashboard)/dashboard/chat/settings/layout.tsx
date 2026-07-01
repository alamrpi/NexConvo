import type { ReactNode } from 'react';

export default function ChatSettingsLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden bg-background">
      {children}
    </div>
  );
}
