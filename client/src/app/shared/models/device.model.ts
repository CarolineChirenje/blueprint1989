export enum InstallPromptStatus {
  Unknown = 'Unknown',
  RemindLater = 'RemindLater',
  Deferred = 'Deferred',
  NeverAskAgain = 'NeverAskAgain',
  Installed = 'Installed'
}

export enum DeviceType {
  Unknown = 'Unknown',
  Android = 'Android',
  iOS = 'iOS',
  Desktop = 'Desktop'
}

export interface RegisterDeviceRequest {
  clientId: string;
  deviceModel?: string;
  deviceManufacturer?: string;
  osVersion?: string;
  appVersion?: string;
  userAgent?: string;
}

export interface RegisterDeviceResponse {
  id: number;
  clientId: string;
  installStatus: InstallPromptStatus;
  nextPromptAt: string | null;
  isNew: boolean;
}

export interface UpdateInstallStatusRequest {
  clientId: string;
  installStatus: number;
  nextPromptAt?: string | null;
}

export interface UserDeviceDto {
  id: number;
  clientId: string;
  deviceType: DeviceType;
  deviceModel: string | null;
  deviceManufacturer: string | null;
  osVersion: string | null;
  appVersion: string | null;
  friendlyName: string | null;
  installStatus: InstallPromptStatus;
  nextPromptAt: string | null;
  isActive: boolean;
  lastSeenAt: string;
  createdAt: string;
}
