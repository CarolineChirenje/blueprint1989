export enum Role {
    SuperAdmin = 1,
    Admin = 2,
    User = 3
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
