export interface AppConfigEntry {
  id: number;
  key: string;
  value: string;
  dataType: 'int' | 'decimal' | 'bool' | 'string' | 'json';
  category: string;
  displayName: string;
  description: string;
  isReadOnly: boolean;
  requiresRestart: boolean;
  isSecret: boolean;
  updatedAt: string;
}

export interface UpdateAppConfigEntryRequest {
  key: string;
  value: string;
}

export interface BulkUpdateAppConfigRequest {
  updates: UpdateAppConfigEntryRequest[];
}

export interface BulkUpdateAppConfigResponse {
  updated: AppConfigEntry[];
  errors: string[];
  hasErrors: boolean;
}
