import { redirect } from 'next/navigation';

/** Settings has no landing of its own yet — go straight to the Email section. */
export default function SettingsIndexPage() {
  redirect('/dashboard/settings/email');
}
