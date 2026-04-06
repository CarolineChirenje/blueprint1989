export enum Role {
    SuperAdmin = 1,
    Admin = 2,
    Member = 3
}

export interface User {
    id?: number;
    email?: string;
    firstName?: string;
    lastName?: string;
    role?: Role;
    roleName?: string;
    isActive?: boolean;
    tourCompleted?: boolean;
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
    createdAt: string;
}

export interface UserGroupMembershipDto {
    groupId: number;
    groupName: string;
    groupRole: 'GroupAdmin' | 'GroupMember';
}
