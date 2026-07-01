import { setupServer } from 'msw/node';

/**
 * Shared MSW server for all Vitest suites (frontend standard S20).
 *
 * Tests register their own request handlers per-case via `server.use(...)`; there are
 * no default handlers, so any un-mocked request fails loudly (`onUnhandledRequest: 'error'`,
 * configured in `vitest.setup.ts`). This single server intercepts both the node-env server
 * API client and the jsdom BFF hooks, so the whole suite mocks the network one consistent way.
 */
export const server = setupServer();
