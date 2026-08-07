import { NextResponse } from 'next/server';
import axios from 'axios';
import { withBff } from '@/shared/api/server/bff';
import { e2eChannelTest } from '@/shared/api/server/e2e-fixtures';
import type { TestChannelConnectionResponse } from '@/features/settings/model/channel-connection.types';

/**
 * Tests the live connectivity of a channel connection without throwing (S15).
 * Failures are represented in the response body (`success: false`) so the client can
 * distinguish a network/gateway error from a provider-side credential failure.
 * Always returns HTTP 200 so the BFF error boundary does not swallow the failure detail.
 */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;

  try {
    const { data } = await api.post<TestChannelConnectionResponse>(
      `/api/v1/channel-connections/${id}/test`,
    );
    return NextResponse.json(data);
  } catch (error) {
    const message =
      axios.isAxiosError(error) && error.response?.data
        ? String(
            (error.response.data as { message?: string; title?: string }).message ??
              (error.response.data as { message?: string; title?: string }).title ??
              'connectionTestFailed',
          )
        : 'connectionTestFailed';

    const result: TestChannelConnectionResponse = { success: false, status: 'Failed', errorMessage: message };
    return NextResponse.json(result);
  }
}, e2eChannelTest);
