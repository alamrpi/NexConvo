'use client';

import { FileBarChart } from 'lucide-react';

export default function ChatReportsPage() {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 text-muted-foreground">
      <FileBarChart className="h-10 w-10 opacity-30" />
      <p className="text-sm">Reports coming soon</p>
    </div>
  );
}
