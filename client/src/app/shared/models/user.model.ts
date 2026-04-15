export enum Role {
    SuperAdmin = 1,
    Admin = 2,
    Member = 3
}

export enum KycStatus {
    NotStarted = 'NotStarted',
    PendingReview = 'PendingReview',
    Verified = 'Verified',
    Rejected = 'Rejected',
    AdminBypassed = 'AdminBypassed'
}

export interface User {
    id?: number;
    email?: string;
    firstName?: string;
    lastName?: string;
    phoneNumber?: string;
    role?: Role;
    roleName?: string;
    isActive?: boolean;
    tourCompleted?: boolean;
    kycStatus?: KycStatus;
}

export interface UserManagementDto {
    id: number;
    email: string;
    firstName: string;
    lastName: string;
    fullName: string;
    role: string;
    roleName: string;
    isActive: boolean;
    activeStatus: string;
    isMfaEnabled: boolean;
    isEmailVerified: boolean;
    createdAt: string;
    kycStatus: KycStatus;
}

export interface UserGroupMembershipDto {
    groupId: number;
    groupName: string;
    groupRole: 'GroupAdmin' | 'GroupMember';
}
