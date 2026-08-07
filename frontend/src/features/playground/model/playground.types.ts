export interface ModelDto {
  modelId: string;
  modelName: string;
}

export interface ProviderModelDto {
  providerId: string;
  providerName: string;
  models: ModelDto[];
}
