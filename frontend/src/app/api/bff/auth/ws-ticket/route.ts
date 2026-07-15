import { NextResponse } from 'next/server';
import { readSession } from '@/shared/api/server/session';

export async function GET() {
  const session = await readSession();
  if (!session) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }
  // In local dev, Next.js and Gateway run on different ports.
  // In production, an Ingress controller routes both Next.js and Gateway under the same domain,
  // so a relative URL will correctly hit the Gateway without exposing internal k8s DNS.
  const baseUrl = process.env.NODE_ENV === 'development' ? process.env.API_GATEWAY_URL : '';

  return NextResponse.json({ 
    ticket: session.accessToken,
    url: `${baseUrl}/hubs/playground`
  });
}
