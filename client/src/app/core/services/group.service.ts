import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  GroupDto,
  GroupDetailDto,
  GroupMemberDto,
  GroupInviteDto,
  CreateGroupRequest,
  UpdateGroupRequest,
  InviteGroupMemberRequest,
  RespondToInviteRequest,
  UpdateGroupMemberRoleRequest,
  JoinByCodeRequest,
  JoinByCodeResponse,
  JoinRequestDto,
  RespondToJoinRequestRequest,
  MyJoinRequestDto,
  AdminPendingJoinRequestDto
} from '../../shared/models/group.model';

@Injectable({ providedIn: 'root' })
export class GroupService {
  private url = `${environment.apiUrl}/groups`;

  constructor(private http: HttpClient) {}

  getGroups(): Observable<GroupDto[]> {
    return this.http.get<GroupDto[]>(this.url);
  }

  getGroup(id: number): Observable<GroupDetailDto> {
    return this.http.get<GroupDetailDto>(`${this.url}/${id}`);
  }

  createGroup(req: CreateGroupRequest): Observable<GroupDto> {
    return this.http.post<GroupDto>(this.url, req);
  }

  updateGroup(id: number, req: UpdateGroupRequest): Observable<GroupDto> {
    return this.http.put<GroupDto>(`${this.url}/${id}`, req);
  }

  deleteGroup(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  getGroupMembers(groupId: number): Observable<GroupMemberDto[]> {
    return this.http.get<GroupMemberDto[]>(`${this.url}/${groupId}/members`);
  }

  inviteMember(groupId: number, req: InviteGroupMemberRequest): Observable<GroupMemberDto> {
    return this.http.post<GroupMemberDto>(`${this.url}/${groupId}/invites`, req);
  }

  removeMember(groupId: number, userId: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${groupId}/members/${userId}`);
  }

  updateMemberRole(groupId: number, userId: number, req: UpdateGroupMemberRoleRequest): Observable<void> {
    return this.http.put<void>(`${this.url}/${groupId}/members/${userId}/role`, req);
  }

  getMyInvites(): Observable<GroupInviteDto[]> {
    return this.http.get<GroupInviteDto[]>(`${this.url}/my-invites`);
  }

  respondToInvite(groupId: number, req: RespondToInviteRequest): Observable<void> {
    return this.http.post<void>(`${this.url}/${groupId}/invites/respond`, req);
  }

  leaveGroup(groupId: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${groupId}/leave`, {});
  }

  // ── Join-by-code ────────────────────────────────────────────────────────

  joinByCode(req: JoinByCodeRequest): Observable<JoinByCodeResponse> {
    return this.http.post<JoinByCodeResponse>(`${this.url}/join`, req);
  }

  regenerateJoinCode(groupId: number): Observable<{ joinCode: string; joinUrl: string | null }> {
    return this.http.post<{ joinCode: string; joinUrl: string | null }>(`${this.url}/${groupId}/regenerate-code`, {});
  }

  getJoinRequests(groupId: number): Observable<JoinRequestDto[]> {
    return this.http.get<JoinRequestDto[]>(`${this.url}/${groupId}/join-requests`);
  }

  respondToJoinRequest(groupId: number, userId: number, req: RespondToJoinRequestRequest): Observable<void> {
    return this.http.post<void>(`${this.url}/${groupId}/join-requests/${userId}/respond`, req);
  }

  getMyJoinRequests(): Observable<MyJoinRequestDto[]> {
    return this.http.get<MyJoinRequestDto[]>(`${this.url}/my-join-requests`);
  }

  cancelJoinRequest(groupId: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${groupId}/cancel-join-request`, {});
  }

  getPendingJoinRequestsForAdmin(): Observable<AdminPendingJoinRequestDto[]> {
    return this.http.get<AdminPendingJoinRequestDto[]>(`${this.url}/pending-join-requests`);
  }
}
