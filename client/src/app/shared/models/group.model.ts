export interface GroupDto {
  id: number;
  name: string;
  description: string | null;
  isActive: boolean;
  memberCount: number;
  createdAt: string;
  canManage: boolean;
}

export interface GroupMemberDto {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  groupRole: string;
  status: string;
  invitedAt: string;
  respondedAt: string | null;
}

export interface GroupDetailDto extends GroupDto {
  members: GroupMemberDto[];
}

export interface GroupInviteDto {
  groupId: number;
  groupName: string;
  invitedByUserId: number;
  invitedByName: string;
  groupRole: string;
  invitedAt: string;
  status: string;
}

export interface CreateGroupRequest {
  name: string;
  description?: string | null;
}

export interface UpdateGroupRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface InviteGroupMemberRequest {
  userId: number;
  groupRole: 'GroupAdmin' | 'GroupMember';
}

export interface RespondToInviteRequest {
  accept: boolean;
}

export interface UpdateGroupMemberRoleRequest {
  groupRole: 'GroupAdmin' | 'GroupMember';
}
