/**
 * Workspace-users contracts — mirror `Application/Users/ListUsers.cs` (UserListItemDto +
 * PagedResult) exactly (S2/S19).
 */

export type UserStatus = 'Active' | 'Disabled';

export interface UserListItem {
  id: string;
  email: string;
  fullName: string;
  status: UserStatus;
  emailVerified: boolean;
  roleId: string | null;
  roleName: string | null;
  createdAt: string;
}

/** `PagedResult<UserListItemDto>`. */
export interface PagedUsers {
  items: UserListItem[];
  total: number;
  page: number;
  pageSize: number;
}
