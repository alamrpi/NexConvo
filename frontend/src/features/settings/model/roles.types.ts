/**
 * Roles & permissions contracts — mirror the Identity service DTOs exactly (S2/S19).
 * Sources: `Application/Roles/{ListRoles,GetPermissionCatalog}.cs`.
 */

export type PermissionCategory = 'Access' | 'Manage' | 'Operations';

/** `PermissionItemDto` — GET /api/v1/roles/permissions. */
export interface PermissionItem {
  key: string;
  module: string;
  category: PermissionCategory;
}

/** `RoleDto` — GET /api/v1/roles. `grantsAll` = the Owner wildcard ("Full access"). */
export interface Role {
  id: string;
  name: string;
  isSystem: boolean;
  grantsAll: boolean;
  permissions: string[];
  memberCount: number;
}
