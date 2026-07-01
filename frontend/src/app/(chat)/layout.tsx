import { redirect } from 'next/navigation';

/** (chat) group is deprecated — all chat routes now live under /dashboard/chat */
export default function ChatGroupLayout() {
  redirect('/dashboard/chat/inbox');
}
