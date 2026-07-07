import type { S3HealthStatus } from './s3-config.schema';

export interface S3ConfigDto {
  id: string;
  bucketName: string;
  region: string;
  hasAccessKey: boolean;
  customEndpoint: string | null;
  pathPrefix: string | null;
  isActive: boolean;
  lastTestStatus: S3HealthStatus;
  lastTestedAt: string | null;
  lastTestError: string | null;
  lastTestLatencyMs: number | null;
}
