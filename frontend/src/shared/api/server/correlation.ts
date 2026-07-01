import 'server-only';

/** The header that carries a request-spanning correlation id (browser → BFF → gateway → service). */
export const CORRELATION_HEADER = 'x-correlation-id';

/**
 * The correlation id to forward to the gateway: the one the browser sent (so one id spans the
 * whole chain and appears in every backend log line), or a fresh one for server-initiated calls
 * (e.g. SSR /me) that have no inbound id.
 */
export function resolveCorrelationId(req?: { headers: Headers }): string {
  return req?.headers.get(CORRELATION_HEADER) || crypto.randomUUID();
}

/** Axios headers carrying the correlation id (+ any extras) for a bare gateway call. */
export function correlationHeaders(
  req?: { headers: Headers },
  extra?: Record<string, string>,
): Record<string, string> {
  return { [CORRELATION_HEADER]: resolveCorrelationId(req), ...extra };
}
