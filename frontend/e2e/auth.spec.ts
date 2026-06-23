import { test, expect, type Page } from '@playwright/test';

/**
 * End-to-end auth checks against the real stack (browser → BFF → gateway → Identity).
 * Each test runs in an isolated browser context, so signup/login/cookies don't leak
 * between tests. Default UI language is English.
 */

const PASSWORD = 'Password123';

/** A unique workspace slug per call so signup never collides with prior runs. */
function uniqueSlug(): string {
  return `pw-${Date.now().toString(36)}-${Math.floor(Math.random() * 1e4)}`;
}

/** Wait until the client-redirected login page has actually loaded + hydrated, so the
 *  React submit handler is attached (otherwise an early click does a native GET submit). */
async function waitForLoginReady(page: Page): Promise<void> {
  await page.waitForURL(/\/login/);
  await page.waitForLoadState('networkidle');
}

/** Sign up a brand-new tenant + owner; resolves once the dashboard is shown. */
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

test.describe('Authentication', () => {
  test('signup lands on the dashboard and stores tokens only in HttpOnly cookies', async ({
    page,
    context,
  }) => {
    await signupFresh(page);
    await expect(page.getByRole('heading', { name: /good to see you/i })).toBeVisible();

    // Tokens are HttpOnly cookies (S3) ...
    const cookies = await context.cookies();
    expect(cookies.find((c) => c.name === 'nx_at')?.httpOnly).toBe(true);
    expect(cookies.find((c) => c.name === 'nx_rt')?.httpOnly).toBe(true);

    // ... and never readable by JS (no document.cookie, no web storage).
    expect(await page.evaluate(() => document.cookie)).not.toContain('nx_');
    const storage = await page.evaluate(() =>
      JSON.stringify({ ...localStorage, ...sessionStorage }),
    );
    expect(storage).not.toMatch(/accessToken|refreshToken|eyJ/);
  });

  test('logout returns to login and re-login works', async ({ page }) => {
    const { slug, email } = await signupFresh(page);

    // Sign out via the user menu (trigger label is the user's name).
    await page.getByRole('button', { name: 'PW User' }).click();
    await page.getByRole('menuitem', { name: 'Sign out' }).click();
    await waitForLoginReady(page);

    // Log back in with the created credentials.
    await page.getByLabel('Workspace').fill(slug);
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('wrong password shows an inline error and stays on login', async ({ page }) => {
    const { slug, email } = await signupFresh(page);
    await page.getByRole('button', { name: 'PW User' }).click();
    await page.getByRole('menuitem', { name: 'Sign out' }).click();
    await waitForLoginReady(page);

    await page.getByLabel('Workspace').fill(slug);
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill('WrongPassword9');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByText(/incorrect workspace, email, or password/i)).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });

  test('duplicate workspace slug shows a "taken" error', async ({ page }) => {
    const { slug } = await signupFresh(page);

    // Try to create the same slug again.
    await page.goto('/signup');
    await page.getByLabel('Company name').fill('Dup Co');
    await page.getByLabel('Workspace URL').fill(slug);
    await page.getByLabel('Your name').fill('Dup User');
    await page.getByLabel('Work email').fill(`dup@${slug}.test`);
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByRole('button', { name: 'Create workspace' }).click();

    await expect(page.getByText(/already taken/i)).toBeVisible();
  });

  test('client-side validation blocks an invalid submission', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Workspace').fill('acme-corp');
    await page.getByLabel('Email').fill('not-an-email');
    await page.getByLabel('Password', { exact: true }).fill('short');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByText(/enter a valid email address/i)).toBeVisible();
    await expect(page.getByText(/at least 8 characters/i)).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });

  test('protected dashboard redirects unauthenticated users to login', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/login/);
  });
});
