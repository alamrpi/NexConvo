import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './src/tests/msw/server';

// MSW lifecycle shared by every suite. Unhandled requests fail the test so no call
// silently escapes the mock boundary (frontend standard S20).
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
