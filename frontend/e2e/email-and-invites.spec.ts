import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * E2E for the per-workspace email + auth-email flows, against the real stack
 * (browser → BFF → Identity) with Mailpit as the SMTP sink.
 */

const PASSWORD = 'Password123';
const MAILPIT_API = 'http://localhost:8025';

function uniqueSlug(): string {
  return `pw-${Date.now().toString(36)}-${Math.floor(Math.random() * 1e4)}`;
}

async function signupFresh(page: Page): Promise<{ slug: string; email: string }> {
  const slug = uniqueSlug();
  const email = `owner@${slug}.test`;
  await page.goto('/signup');
  await page.getByLabel('Company name').fill('PW Co');
  await page.getByLabel('Workspace URL').fill(slug);
  await page.getByLabel('Your name').fill('PW User');
  await page.getByLabel('Work email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Create workspace' }).click();
  await expect(page).toHaveURL(/\/dashboard/);
  return { slug, email };
}

/** Poll Mailpit for the latest message to `email` and pull the token from its link. */
async function tokenFromEmail(request: APIRequestContext, email: string, linkPath: string): Promise<string> {
  const pattern = new RegExp(`${linkPath}\\?token=([^"&\\s]+)`, 'i');
  for (let attempt = 0; attempt < 20; attempt++) {
    const res = await request.get(`${MAILPIT_API}/api/v1/search?query=to%3A${encodeURIComponent(email)}`);
    if (res.ok()) {
      const body = await res.json();
      if (Array.isArray(body.messages) && body.messages.length > 0) {
        const full = await request.get(`${MAILPIT_API}/api/v1/message/${body.messages[0].ID}`);
        const html = (await full.json()).HTML as string;
        const captured = pattern.exec(html ?? '')?.[1];
        if (captured) {
          return decodeURIComponent(captured);
        }
      }
    }
    await new Promise((r) => setTimeout(r, 400));
  }
  throw new Error(`No ${linkPath} email captured for ${email}`);
}

test.describe('Workspace email settings', () => {
  test('configure SMTP, save, and a test email lands in Mailpit', async ({ page, request }) => {
    const { email } = await signupFresh(page);

    await page.goto('/dashboard/settings/email');
    await expect(page.getByRole('heading', { name: 'Email', exact: true })).toBeVisible();

    await page.getByLabel('From name').fill('PW Support');
    await page.getByLabel('From email').fill('no-reply@pw.test');
    await page.getByLabel('SMTP host').fill('localhost');
    await page.getByLabel('Port').fill('1025');
    await page.getByLabel('Use SSL/TLS').uncheck();
    await page.getByLabel(/Enable email sending/i).check();
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Settings saved.')).toBeVisible();

    await page.getByRole('button', { name: 'Send test email' }).click();
    await expect(page.getByText(/Test email sent/i)).toBeVisible();

    await expect
      .poll(
        async () => {
          const res = await request.get(`${MAILPIT_API}/api/v1/search?query=to%3A${encodeURIComponent(email)}`);
          return res.ok() ? (await res.json()).messages_count : 0;
        },
        { timeout: 15_000 },
      )
      .toBeGreaterThan(0);
  });
});

test.describe('Members & invitations', () => {
  test('inviting a teammate shows it in the pending list', async ({ page }) => {
    await signupFresh(page);
    const invitee = `mate-${Date.now().toString(36)}@pw.test`;

    await page.goto('/dashboard/settings/members');
    await expect(page.getByRole('heading', { name: 'Members', exact: true })).toBeVisible();

    await page.getByLabel('Email').fill(invitee);
    await page.getByRole('button', { name: 'Send invite' }).click();

    await expect(page.getByText('Invitation sent.')).toBeVisible();
    await expect(page.getByText(invitee)).toBeVisible();
  });
});

test.describe('Password reset', () => {
  test('forgot → email link → reset → log in with the new password', async ({ page, request }) => {
    const { slug, email } = await signupFresh(page);

    await page.getByRole('button', { name: 'PW User' }).click();
    await page.getByRole('menuitem', { name: 'Sign out' }).click();
    await page.waitForURL(/\/login/);

    await page.goto('/forgot-password');
    await page.getByLabel('Workspace').fill(slug);
    await page.getByLabel('Email').fill(email);
    await page.getByRole('button', { name: 'Send reset link' }).click();
    await expect(page.getByText(/reset link is on its way/i)).toBeVisible();

    const token = await tokenFromEmail(request, email, '/reset-password');
    await page.goto(`/reset-password?token=${encodeURIComponent(token)}`);

    const newPassword = 'BrandNewPass123';
    await page.getByLabel('New password').fill(newPassword);
    await page.getByLabel('Confirm password').fill(newPassword);
    await page.getByRole('button', { name: 'Reset password' }).click();
    await page.waitForURL(/\/login/);

    await page.getByLabel('Workspace').fill(slug);
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(newPassword);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).toHaveURL(/\/dashboard/);
  });
});
