export interface NotificationPreferenceDto {
  typeId: number;
  typeName: string;
  description: string | null;
  isEnabled: boolean;
  /** When true the user cannot toggle this — only an admin can. */
  isAdminControlled: boolean;
}

export interface UpdateNotificationPreferenceItem {
  typeId: number;
  isEnabled: boolean;
}

export interface UpdateNotificationPreferencesRequest {
  preferences: UpdateNotificationPreferenceItem[];
}
