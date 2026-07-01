/**
 * Settings sub-layout — no extra chrome needed here; the chat (chat)/layout.tsx
 * already provides the outer shell and settings nav links appear in ChatNav.
 */
export default function ChatSettingsLayout({ children }: { children: React.ReactNode }) {
  return <div className="h-full overflow-auto">{children}</div>;
}
