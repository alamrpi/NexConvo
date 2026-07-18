import { NextRequest, NextResponse } from 'next/server';
import { readSession } from '@/shared/api/server/session';

// The set of hubs this route is allowed to mint a ticket for. Keep in lockstep with the
// Gateway's YARP routes under /hubs/{**catch-all} (src/gateway/NexConvo.Gateway/appsettings.json) —
// adding a hub here without a matching Gateway/hub route just yields a connection failure, not a
// security issue, since the hub name only selects the target path.
const ALLOWED_HUBS = ['playground', 'chat'] as const;
type HubName = (typeof ALLOWED_HUBS)[number];

function isHubName(value: string | null): value is HubName {
  return value !== null && (ALLOWED_HUBS as readonly string[]).includes(value);
}

// NOTE: this hands the real access token to client JS so the browser can put it in the
// WebSocket `?access_token=` query string — the only way SignalR can authenticate a WS
// handshake, since browsers cannot set an Authorization header on it. This is the same
// mechanism the Gateway/Chat JWT hooks already accept for every hub (see their
// OnMessageReceived comments). It is a real, bounded exposure (token lifetime =
// access-token TTL, not the 14-day refresh token) rather than the HttpOnly-only ideal in
// session.ts — replacing it with a short-lived, single-use ticket needs a dedicated
// ticket-issuing endpoint + Gateway/hub validation change and is tracked separately.
//
// ?hub=chat|playground selects which hub the ticket's url points at (default "playground" so the
// existing playground caller — frontend/src/app/(dashboard)/dashboard/chat/playground/page.tsx —
// keeps working unchanged with no query string).
export async function GET(request: NextRequest) {
  const session = await readSession();
  if (!session) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const requestedHub = request.nextUrl.searchParams.get('hub');
  const hub: HubName = isHubName(requestedHub) ? requestedHub : 'playground';

  const gatewayUrl = process.env.API_GATEWAY_URL;
  if (process.env.NODE_ENV === 'development' && !gatewayUrl) {
    return NextResponse.json({ error: 'API_GATEWAY_URL is not configured' }, { status: 500 });
  }

  // In local dev, Next.js and Gateway run on different ports.
  // In production, an Ingress controller routes both Next.js and Gateway under the same domain,
  // so a relative URL will correctly hit the Gateway without exposing internal k8s DNS.
  const baseUrl = process.env.NODE_ENV === 'development' ? gatewayUrl : '';

  return NextResponse.json({
    ticket: session.accessToken,
    url: `${baseUrl}/hubs/${hub}`,
  });
}
