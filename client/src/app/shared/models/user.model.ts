export enum Role {
    SuperAdmin = 1,
    Admin = 2,
    Member = 3
}

export enum KycStatus {
    NotStarted = 0,
    PendingReview = 1,
    Verified = 2,
    Rejected = 3,
    AdminBypassed = 4
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
}

export interface UserGroupMembershipDto {
    groupId: number;
    groupName: string;
    groupRole: 'GroupAdmin' | 'GroupMember';
}
