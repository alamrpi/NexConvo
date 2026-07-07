import { describe, it, expect } from 'vitest';
import { connectionHealthResultSchema, connectionHealthStatusSchema } from './connection-health.schema';

describe('connectionHealthStatusSchema', () => {
  it.each(['Untested', 'Healthy', 'Degraded', 'Failed'])('accepts the "%s" enum member name', (value) => {
    expect(connectionHealthStatusSchema.safeParse(value).success).toBe(true);
  });

  it('rejects a numeric enum value (backend serializes the enum as a string)', () => {
    expect(connectionHealthStatusSchema.safeParse(1).success).toBe(false);
  });

  it('rejects an unknown status string', () => {
    expect(connectionHealthStatusSchema.safeParse('Unknown').success).toBe(false);
  });
});

describe('connectionHealthResultSchema', () => {
  it('parses a successful test result', () => {
    const result = connectionHealthResultSchema.safeParse({
      success: true,
      status: 'Healthy',
      detail: 'Reachable',
      errorMessage: null,
      latencyMs: 120,
    });
    expect(result.success).toBe(true);
  });

  it('parses a failed test result with optional fields omitted', () => {
    const result = connectionHealthResultSchema.safeParse({
      success: false,
      status: 'Failed',
      errorMessage: 'Invalid API key',
    });
    expect(result.success).toBe(true);
  });

  it('rejects a missing status', () => {
    const result = connectionHealthResultSchema.safeParse({ success: true });
    expect(result.success).toBe(false);
  });
});
