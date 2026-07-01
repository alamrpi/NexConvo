import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { profileSchema } from '@/features/account/model/profile.schema';

/** Update the signed-in user's profile. Re-validates with the shared schema (S10). */
export const PUT = withBff(async (req, { api }) => {
  const parsed = profileSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  await api.put('/api/v1/account/profile', parsed.data);
  return new NextResponse(null, { status: 204 });
});
