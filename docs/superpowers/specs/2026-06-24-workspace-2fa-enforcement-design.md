# Workspace-wide 2FA Enforcement — Design

**Date:** 2026-06-24 · **Branch:** `feat/workspace-email` · **Status:** approved (user said "go", autonomous build)

## Context

NexConvo already supports per-user opt-in TOTP 2FA and a **soft email-verification** model:
sensitive writes (`IRequireVerifiedActor` commands — invite, role create/update/delete,
change-user-role, deactivate/reactivate, settings writes) are blocked with **403** until the
acting user verifies their email, enforced by a MediatR pipeline behavior
(`VerifiedActorBehavior`) that checks **live DB state**.

This feature lets a workspace Owner **require 2FA for the whole workspace**. It reuses the exact
soft-enforcement machinery so the behavior is consistent and low-risk.

## Locked decisions

1. **Enforcement = soft gate (writes only).** Members log in and read/work normally, but their
   `IRequireVerifiedActor` writes return **403** until they enroll in 2FA — only when their
   workspace has 2FA required. No login changes, no forced-enrollment session.
2. **No precondition on the toggle.** An Owner can enable workspace 2FA even if they haven't
   enrolled themselves (they'll then be soft-gated on writes like everyone else).
3. **YAGNI:** a single workspace boolean (no per-role policy), no MFA beyond existing TOTP.

## Backend (Identity)

- **Storage:** `Tenant.RequireTwoFactor` (bool, default `false`) + `SetRequireTwoFactor(bool)`.
  EF column `require_two_factor` (snake_case) with DB default `false`; one migration. `Tenant`
  is the non-RLS registry root (login resolves it pre-tenant-context), so it's the natural home.
- **Enforcement:** new `RequiredTwoFactorActorBehavior<TRequest,TResponse>` for
  `IRequireVerifiedActor` requests (same set the email gate covers). Live check: load the actor's
  `TwoFactorEnabled` + `TenantId`, and if `!TwoFactorEnabled` and the tenant's `RequireTwoFactor`
  is on → throw `TwoFactorRequiredException` (BuildingBlocks.Domain), mapped to **403** at the
  edge. Data-driven (the per-tenant flag is the switch; no AuthOptions flag). Registered in
  `DependencyInjection.cs` via `AddOpenBehavior` after `VerifiedActorBehavior`.
- **Edge mapping:** extend `ExceptionHandlingMiddleware` to emit a machine-readable `code` in the
  problem body — `email-not-verified` for `EmailNotVerifiedException`, `two-factor-required` for
  `TwoFactorRequiredException` — so the BFF/frontend can distinguish the two 403s.
- **Toggle endpoint:** `GET /api/v1/settings/security` + `PUT /api/v1/settings/security`
  `{ requireTwoFactor }` on the existing `SettingsController` (class-level `settings:manage`).
  CQRS `GetSecuritySettingsQuery` / `UpdateSecuritySettingsCommand(requireTwoFactor, actor)`,
  audited (`workspace.2fa_required_changed`). **The command does NOT implement
  `IRequireVerifiedActor`** — otherwise enabling the requirement would self-gate (contradicting
  "no precondition"). Only RBAC-gated.
- **/me:** add `WorkspaceRequiresTwoFactor` (bool) to `CurrentUserDto`, populated from the tenant
  the handler already loads. `login` is unchanged.
- **Gateway:** no change — `/api/v1/settings/{**}` already routes to identity.

## Frontend

- **`shared/ui/switch.tsx`** (the deferred Radix switch) + `@radix-ui/react-switch`.
- **`CurrentUser`** type gains `workspaceRequiresTwoFactor: boolean` (mirror `/me`).
- **Security settings:** `features/settings/model/security-settings.types.ts`,
  `api/use-security-settings.ts` (GET + PUT), BFF `app/api/bff/settings/security/route.ts`, and a
  `workspace-two-factor-policy.tsx` card on `/dashboard/settings/security` (gated `settings:manage`)
  — a Switch with the live state, optimistic-free invalidate, 3 async states.
- **Prompt banner:** `two-factor-required-banner.tsx` (mirrors `email-verification-banner`) shown
  when `workspaceRequiresTwoFactor && !user.twoFactorEnabled`, linking to the security page. Mounted
  in the dashboard shell beside the email banner.
- **403 handling:** `use-role-mutations` / `use-user-mutations` / invite map a 403 with
  `code:'two-factor-required'` to a distinct `twoFactorRequired` message (else `unverified`).
- **i18n:** `settings.security.workspacePolicy.*`, the `twoFactorRequired` error key, and the banner
  copy in **en + bn** (parity green).

## Testing

- **Backend:** `RequiredTwoFactorActorBehavior` blocks a gated write when required + unenrolled (403),
  allows it after enrolling, and is a no-op when the flag is off; `[Trait("Category","Security")]`.
  Toggle persists + is audited; the toggle itself works for an un-enrolled Owner (no precondition).
- **Frontend:** security-settings hook (MSW), banner visibility logic, 403→`twoFactorRequired`
  mapping. `messages.test.ts` parity stays green.

## Out of scope (later)

Forced-enrollment / hard-block modes; per-role 2FA policy; "log out all devices"; the deferred
identity backlog (expired-token cleanup, email outbox, Bengali email bodies, SSO).
