export interface S3ConfigDto {
  id: string;
  bucketName: string;
  region: string;
  hasAccessKey: boolean;
  customEndpoint: string | null;
  pathPrefix: string | null;
  isActive: boolean;
}
