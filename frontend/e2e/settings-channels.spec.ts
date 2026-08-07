import { test, expect, type Page } from '@playwright/test';

/**
 * E2E tests for /dashboard/chat/settings/channels.
 *
 * Auth: nx_e2e_bypass cookie (NEXT_PUBLIC_E2E=true in .env.e2e.local bypasses the
 * middleware JWT check — same pattern as chat-inbox.spec.ts).
 *
 * The channels page renders a vertical list of 5 channels in their initial mock state:
 *   WhatsApp   — connected
 *   Facebook   — not_connected
 *   Instagram  — error
 *   Telegram   — not_connected
 *   Web Widget — connected
 *
 * ConnectDrawer.handleVerify and handleSendTest both always succeed in this build
 * (deterministic stubs). Disconnect is immediate (no confirmation dialog) — clicking
 * Disconnect calls handleDisconnect which sets the channel to not_connected directly.
 */

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function gotoChannels(page: Page): Promise<void> {
  await page.context().addCookies([
    { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
  ]);
  await page.goto('/dashboard/chat/settings/channels');
  await expect(page.getByRole('heading', { name: 'Channel Connections' })).toBeVisible();
}

/**
 * Walk the ConnectDrawer through all 5 steps and close it.
 * Works for any channel (credentials step is skipped by clicking Next without filling).
 * Leaves the drawer closed and status set to 'connected'.
 */
async function walkThroughConnectDrawer(page: Page): Promise<void> {
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Step 1 — Credentials (no credentials required to advance)
  await expect(dialog.getByRole('heading', { name: 'Credentials' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Next' }).click();

  // Step 2 — Verify
  await expect(dialog.getByRole('heading', { name: 'Verify' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Verify Credentials' }).click();
  await expect(dialog.getByText('Credentials verified!')).toBeVisible({ timeout: 5_000 });
  await dialog.getByRole('button', { name: 'Next' }).click();

  // Step 3 — Webhook
  await expect(dialog.getByRole('heading', { name: 'Webhook' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Next' }).click();

  // Step 4 — Test
  await expect(dialog.getByRole('heading', { name: 'Test' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Send Test Message' }).click();
  await expect(dialog.getByText('Test message received!')).toBeVisible({ timeout: 5_000 });
  await dialog.getByRole('button', { name: 'Next' }).click();

  // Step 5 — Done
  await expect(dialog.getByText('Connected successfully!')).toBeVisible();
  await dialog.getByRole('button', { name: 'Close' }).click();

  await expect(dialog).not.toBeVisible();
}

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('Settings — Channels', () => {
  test('shows page heading and subtitle', async ({ page }) => {
    await gotoChannels(page);
    await expect(page.getByRole('heading', { name: 'Channel Connections' })).toBeVisible();
    await expect(
      page.getByText('Connect your messaging channels to start receiving conversations.'),
    ).toBeVisible();
  });

  test('shows all 5 channel names', async ({ page }) => {
    await gotoChannels(page);
    for (const name of ['WhatsApp', 'Facebook', 'Instagram', 'Telegram', 'Web Widget']) {
      await expect(page.getByText(name).first()).toBeVisible();
    }
  });

  test('not-connected channels show "Not connected" badge and Connect button', async ({ page }) => {
    await gotoChannels(page);

    // Facebook and Telegram start as not_connected in the mock data
    for (const channelName of ['Facebook', 'Telegram']) {
      const row = page
        .locator('div')
        .filter({ has: page.getByText(channelName, { exact: true }) })
        .first();
      await expect(row.getByText('Not connected')).toBeVisible();
      await expect(row.getByRole('button', { name: 'Connect' })).toBeVisible();
    }
  });

  test('connected channels show "Connected" badge, Configure, and Disconnect buttons', async ({ page }) => {
    await gotoChannels(page);

    // WhatsApp and Web Widget start as connected in the mock data
    for (const channelName of ['WhatsApp', 'Web Widget']) {
      const row = page
        .locator('div')
        .filter({ has: page.getByText(channelName, { exact: true }) })
        .first();
      await expect(row.getByText('Connected')).toBeVisible();
      await expect(row.getByRole('button', { name: 'Configure' })).toBeVisible();
      await expect(row.getByRole('button', { name: 'Disconnect' })).toBeVisible();
    }
  });

  test('error channel shows "Connection error" badge, Reconnect button, and inline error message', async ({
    page,
  }) => {
    await gotoChannels(page);

    // Instagram starts with status 'error' in the mock data
    const row = page
      .locator('div')
      .filter({ has: page.getByText('Instagram', { exact: true }) })
      .first();
    await expect(row.getByText('Connection error')).toBeVisible();
    await expect(row.getByRole('button', { name: 'Reconnect' })).toBeVisible();
    await expect(page.getByText('Token expired — please reconnect')).toBeVisible();
  });

  test('clicking Connect opens the drawer showing the channel name and step indicator', async ({ page }) => {
    await gotoChannels(page);

    const facebookRow = page
      .locator('div')
      .filter({ has: page.getByText('Facebook', { exact: true }) })
      .first();
    await facebookRow.getByRole('button', { name: 'Connect' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('Facebook')).toBeVisible();
    await expect(page.getByLabel('Connection steps')).toBeVisible();
  });

  test('closing the drawer via × button leaves channel status unchanged', async ({ page }) => {
    await gotoChannels(page);

    const facebookRow = page
      .locator('div')
      .filter({ has: page.getByText('Facebook', { exact: true }) })
      .first();
    await facebookRow.getByRole('button', { name: 'Connect' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Close drawer' }).click();
    await expect(dialog).not.toBeVisible();

    // Facebook must still be not_connected
    await expect(facebookRow.getByText('Not connected')).toBeVisible();
  });

  test('can complete the full 5-step connect flow for Facebook', async ({ page }) => {
    await gotoChannels(page);

    const facebookRow = page
      .locator('div')
      .filter({ has: page.getByText('Facebook', { exact: true }) })
      .first();
    await facebookRow.getByRole('button', { name: 'Connect' }).click();

    await walkThroughConnectDrawer(page);

    // After Close, the row must show Connected
    await expect(facebookRow.getByText('Connected')).toBeVisible();
    await expect(facebookRow.getByRole('button', { name: 'Configure' })).toBeVisible();
    await expect(facebookRow.getByRole('button', { name: 'Disconnect' })).toBeVisible();
    await expect(facebookRow.getByRole('button', { name: 'Connect' })).not.toBeVisible();
  });

  test('drawer step 2 Next button is disabled until Verify Credentials is clicked', async ({ page }) => {
    await gotoChannels(page);

    const telegramRow = page
      .locator('div')
      .filter({ has: page.getByText('Telegram', { exact: true }) })
      .first();
    await telegramRow.getByRole('button', { name: 'Connect' }).click();

    const dialog = page.getByRole('dialog');
    // Advance to step 2
    await dialog.getByRole('button', { name: 'Next' }).click();
    await expect(dialog.getByRole('heading', { name: 'Verify' })).toBeVisible();

    const nextBtn = dialog.getByRole('button', { name: 'Next' });
    await expect(nextBtn).toBeDisabled();

    await dialog.getByRole('button', { name: 'Verify Credentials' }).click();
    await expect(dialog.getByText('Credentials verified!')).toBeVisible({ timeout: 5_000 });
    await expect(nextBtn).toBeEnabled();
  });

  test('drawer step 3 shows the webhook URL in a code element and a Copy URL button', async ({ page }) => {
    await gotoChannels(page);

    const telegramRow = page
      .locator('div')
      .filter({ has: page.getByText('Telegram', { exact: true }) })
      .first();
    await telegramRow.getByRole('button', { name: 'Connect' }).click();

    const dialog = page.getByRole('dialog');

    // Step 1 → 2 → 3
    await dialog.getByRole('button', { name: 'Next' }).click();
    await dialog.getByRole('button', { name: 'Verify Credentials' }).click();
    await expect(dialog.getByText('Credentials verified!')).toBeVisible({ timeout: 5_000 });
    await dialog.getByRole('button', { name: 'Next' }).click();
    // Now on step 3 — Webhook
    await expect(dialog.getByRole('heading', { name: 'Webhook' })).toBeVisible();
    const webhookCode = dialog.locator('code');
    await expect(webhookCode).toBeVisible();
    await expect(webhookCode).toContainText('nexconvo.app/webhooks/telegram');
    await expect(dialog.getByRole('button', { name: /Copy URL/i })).toBeVisible();
  });

  test('drawer step 4 Next button is disabled until Send Test Message is clicked', async ({ page }) => {
    await gotoChannels(page);

    const telegramRow = page
      .locator('div')
      .filter({ has: page.getByText('Telegram', { exact: true }) })
      .first();
    await telegramRow.getByRole('button', { name: 'Connect' }).click();

    const dialog = page.getByRole('dialog');

    // Steps 1 → 2 → 3 → 4
    await dialog.getByRole('button', { name: 'Next' }).click();
    await dialog.getByRole('button', { name: 'Verify Credentials' }).click();
    await expect(dialog.getByText('Credentials verified!')).toBeVisible({ timeout: 5_000 });
    await dialog.getByRole('button', { name: 'Next' }).click();
    await dialog.getByRole('button', { name: 'Next' }).click();

    await expect(dialog.getByRole('heading', { name: 'Test' })).toBeVisible();
    const nextBtn = dialog.getByRole('button', { name: 'Next' });
    await expect(nextBtn).toBeDisabled();

    await dialog.getByRole('button', { name: 'Send Test Message' }).click();
    await expect(dialog.getByText('Test message received!')).toBeVisible({ timeout: 5_000 });
    await expect(nextBtn).toBeEnabled();
  });

  test('clicking Configure on Web Widget toggles the Widget Appearance panel inline', async ({ page }) => {
    await gotoChannels(page);

    // Web Widget is already connected — Configure toggles the inline WebWidgetConfig
    const webRow = page
      .locator('div')
      .filter({ has: page.getByText('Web Widget', { exact: true }) })
      .first();

    // Open
    await webRow.getByRole('button', { name: 'Configure' }).click();
    await expect(page.getByText('Widget Appearance')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bottom right' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bottom left' })).toBeVisible();
  });

  test('clicking Configure again on Web Widget collapses the panel', async ({ page }) => {
    await gotoChannels(page);

    const webRow = page
      .locator('div')
      .filter({ has: page.getByText('Web Widget', { exact: true }) })
      .first();

    await webRow.getByRole('button', { name: 'Configure' }).click();
    await expect(page.getByText('Widget Appearance')).toBeVisible();

    await webRow.getByRole('button', { name: 'Configure' }).click();
    await expect(page.getByText('Widget Appearance')).not.toBeVisible();
  });

  test('clicking Disconnect immediately moves the channel to not_connected', async ({ page }) => {
    await gotoChannels(page);

    // WhatsApp starts as connected
    const whatsappRow = page
      .locator('div')
      .filter({ has: page.getByText('WhatsApp', { exact: true }) })
      .first();
    await expect(whatsappRow.getByText('Connected')).toBeVisible();

    await whatsappRow.getByRole('button', { name: 'Disconnect' }).click();

    // State transition is synchronous — no dialog
    await expect(whatsappRow.getByText('Not connected')).toBeVisible();
    await expect(whatsappRow.getByRole('button', { name: 'Connect' })).toBeVisible();
    await expect(whatsappRow.getByRole('button', { name: 'Disconnect' })).not.toBeVisible();
  });

  test('can re-connect a channel after disconnecting it', async ({ page }) => {
    await gotoChannels(page);

    const whatsappRow = page
      .locator('div')
      .filter({ has: page.getByText('WhatsApp', { exact: true }) })
      .first();

    // Disconnect first
    await whatsappRow.getByRole('button', { name: 'Disconnect' }).click();
    await expect(whatsappRow.getByText('Not connected')).toBeVisible();

    // Reconnect via the full drawer flow
    await whatsappRow.getByRole('button', { name: 'Connect' }).click();
    await walkThroughConnectDrawer(page);

    await expect(whatsappRow.getByText('Connected')).toBeVisible();
    await expect(whatsappRow.getByRole('button', { name: 'Configure' })).toBeVisible();
  });
});
