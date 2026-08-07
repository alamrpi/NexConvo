import { describe, it, expect } from 'vitest';
import { s3ConfigSchema, s3HealthStatusSchema, s3TestResultSchema } from './s3-config.schema';

describe('s3HealthStatusSchema', () => {
  it.each(['Untested', 'Healthy', 'Degraded', 'Failed'])('accepts the "%s" enum member name', (value) => {
    expect(s3HealthStatusSchema.safeParse(value).success).toBe(true);
  });

  it('rejects a numeric enum value (backend serializes the enum as a string)', () => {
    expect(s3HealthStatusSchema.safeParse(1).success).toBe(false);
  });

  it('rejects an unknown status string', () => {
    expect(s3HealthStatusSchema.safeParse('Unknown').success).toBe(false);
  });
});

describe('s3TestResultSchema', () => {
  it('parses a successful test result', () => {
    const result = s3TestResultSchema.safeParse({
      success: true,
      status: 'Healthy',
      detail: 'Bucket reachable',
      errorMessage: null,
      latencyMs: 120,
    });
    expect(result.success).toBe(true);
  });

  it('parses a failed test result with optional fields omitted', () => {
    const result = s3TestResultSchema.safeParse({
      success: false,
      status: 'Failed',
      errorMessage: 'Access denied',
    });
    expect(result.success).toBe(true);
  });

  it('rejects a missing status', () => {
    const result = s3TestResultSchema.safeParse({ success: true });
    expect(result.success).toBe(false);
  });
});

describe('s3ConfigSchema', () => {
  it('still validates form input unaffected by the new DTO health fields', () => {
    const result = s3ConfigSchema.safeParse({
      bucketName: 'my-nexconvo-bucket',
      region: 'us-east-1',
      isActive: true,
    });
    expect(result.success).toBe(true);
  });
});
