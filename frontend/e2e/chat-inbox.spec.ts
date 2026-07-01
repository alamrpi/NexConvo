import { test, expect, type Page } from '@playwright/test';

/**
 * Inbox UI tests — E2E mode (NEXT_PUBLIC_E2E=true), no real auth needed.
 *
 * Note: ConversationListPanel renders twice (desktop + mobile Drawer), so
 * two search inputs and two tab bars exist in the DOM. We always target .first()
 * or use the visible desktop panel via `.md:flex` parent scoping.
 */

async function gotoInbox(page: Page) {
  await page.context().addCookies([
    { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
  ]);
  await page.goto('/dashboard/chat/inbox');
  await page.waitForSelector('.flex.flex-col.gap-px', { timeout: 15_000 });
}

/**
 * Inbox search input in the visible desktop left panel.
 * The mobile drawer also has one, so we take the first() which is always the
 * desktop panel's input (desktop panel renders before the Drawer in DOM order).
 */
function inboxSearch(page: Page) {
  return page.locator('input[aria-label="Search conversations…"]').first();
}

/**
 * The desktop left panel — `hidden w-72 ... md:flex md:flex-col`.
 * At the test viewport (1280px) it renders as flex; the mobile Drawer is separate.
 */
function desktopPanel(page: Page) {
  return page.locator('.hidden.w-72.shrink-0');
}

function listItems(page: Page) {
  return desktopPanel(page).locator('.flex.flex-col.gap-px > button');
}

/** Click the first item in the desktop list panel. */
async function openFirst(page: Page) {
  const first = listItems(page).first();
  await first.waitFor({ state: 'visible' });
  await first.click();
  await page.waitForTimeout(500);
}

/**
 * The message thread scroll container — uniquely identified by `px-4` class
 * which is only on the message area, not the list panel scroll.
 */
function msgThread(page: Page) {
  return page.locator('.overflow-y-auto.overscroll-contain.px-4');
}

// ─── Layout & scroll containment ─────────────────────────────────────────────

test.describe('Inbox layout', () => {
  test('no page-level scroll — html/body fits viewport', async ({ page }) => {
    await gotoInbox(page);

    const overflows = await page.evaluate(() => ({
      htmlTaller: document.documentElement.scrollHeight > window.innerHeight + 2,
      bodyTaller:  document.body.scrollHeight > window.innerHeight + 2,
    }));

    expect(overflows.htmlTaller, 'html must not exceed viewport height').toBe(false);
    expect(overflows.bodyTaller, 'body must not exceed viewport height').toBe(false);
  });

  test('conversation list scroll area fits within viewport', async ({ page }) => {
    await gotoInbox(page);

    const listScroll = page.locator('.min-h-0.flex-1.overflow-y-auto').first();
    await expect(listScroll).toBeVisible();

    const box = await listScroll.boundingBox();
    const vh  = page.viewportSize()?.height ?? 768;
    expect(box?.height, 'list area height must not exceed viewport').toBeLessThanOrEqual(vh + 2);
  });

  test('Inbox heading stays fixed when list scrolls', async ({ page }) => {
    await gotoInbox(page);

    const heading = page.locator('h1', { hasText: 'Inbox' }).first();
    const before  = await heading.boundingBox();

    await page.locator('.min-h-0.flex-1.overflow-y-auto').first()
      .evaluate((el) => el.scrollBy(0, 300));
    await page.waitForTimeout(100);

    const after = await heading.boundingBox();
    expect(after?.y).toBeCloseTo(before?.y ?? 0, 0);
  });

  test('conversation list renders items', async ({ page }) => {
    await gotoInbox(page);
    await expect(listItems(page).first()).toBeVisible();
    expect(await listItems(page).count()).toBeGreaterThan(0);
  });
});

// ─── Conversation view ────────────────────────────────────────────────────────

test.describe('Conversation view', () => {
  test('clicking a conversation shows the message thread', async ({ page }) => {
    await gotoInbox(page);
    await openFirst(page);

    await expect(msgThread(page)).toBeVisible();
    const header = page.locator('header').last();
    await expect(header).toBeVisible();
  });

  test('conversation header stays fixed while messages scroll', async ({ page }) => {
    await gotoInbox(page);
    await openFirst(page);

    const header = page.locator('header').last();
    await expect(header).toBeVisible();
    const before = await header.boundingBox();

    await msgThread(page).evaluate((el) => { el.scrollTop = 0; });
    await page.waitForTimeout(150);

    const after = await header.boundingBox();
    expect(after?.y).toBeCloseTo(before?.y ?? 0, 0);
  });

  test('message area auto-scrolls to bottom on open', async ({ page }) => {
    await gotoInbox(page);
    await openFirst(page);
    await page.waitForTimeout(700);

    const atBottom = await msgThread(page).evaluate((el) => {
      return el.scrollHeight - el.scrollTop - el.clientHeight < 20;
    });
    expect(atBottom, 'message thread should be at bottom').toBe(true);
  });

  test('no page-level scroll after opening a conversation', async ({ page }) => {
    await gotoInbox(page);
    await openFirst(page);
    await page.waitForTimeout(200);

    const tall = await page.evaluate(() =>
      document.documentElement.scrollHeight > window.innerHeight + 2,
    );
    expect(tall, 'page must not scroll after opening a conversation').toBe(false);
  });

  test('message thread height does not exceed viewport', async ({ page }) => {
    await gotoInbox(page);
    await openFirst(page);

    const box = await msgThread(page).boundingBox();
    const vh  = page.viewportSize()?.height ?? 768;
    expect(box?.height, 'message area must fit within viewport').toBeLessThanOrEqual(vh + 2);
  });
});

// ─── Composer and state transitions ──────────────────────────────────────────

test.describe('Composer and state transitions', () => {
  async function openPending(page: Page) {
    await gotoInbox(page);
    // The tabs render twice; take the first (desktop panel)
    await page.locator('[role="tab"]', { hasText: /^Pending/ }).first().click();
    await page.waitForTimeout(300);
    await listItems(page).first().click();
    await page.waitForTimeout(500);
  }

  test('Pending conversation shows Accept button', async ({ page }) => {
    await openPending(page);
    await expect(page.getByRole('button', { name: /^accept$/i })).toBeVisible();
  });

  test('Accept transitions to composer', async ({ page }) => {
    await openPending(page);
    await page.getByRole('button', { name: /^accept$/i }).click();
    await page.waitForTimeout(400);
    await expect(page.locator('textarea[aria-label="Compose message"]')).toBeVisible();
  });

  test('composer sits below the message thread', async ({ page }) => {
    await openPending(page);
    await page.getByRole('button', { name: /^accept$/i }).click();
    await page.waitForTimeout(400);

    const composerBox = await page.locator('textarea[aria-label="Compose message"]').boundingBox();
    expect(composerBox).not.toBeNull();

    const threadBox = await msgThread(page).boundingBox();
    expect(threadBox).not.toBeNull();

    const threadBottom = (threadBox?.y ?? 0) + (threadBox?.height ?? 0);
    expect(composerBox!.y).toBeGreaterThanOrEqual(threadBottom - 2);
  });

  test('composer Y position is stable when messages scroll', async ({ page }) => {
    await openPending(page);
    await page.getByRole('button', { name: /^accept$/i }).click();
    await page.waitForTimeout(400);

    const composer = page.locator('textarea[aria-label="Compose message"]');
    const yBefore = (await composer.boundingBox())?.y;

    await msgThread(page).evaluate((el) => { el.scrollTop = 0; });
    await page.waitForTimeout(150);

    const yAfter = (await composer.boundingBox())?.y;
    expect(yAfter).toBeCloseTo(yBefore ?? 0, 0);
  });

  test('Enter sends and clears the composer', async ({ page }) => {
    await openPending(page);
    await page.getByRole('button', { name: /^accept$/i }).click();
    await page.waitForTimeout(400);

    const composer = page.locator('textarea[aria-label="Compose message"]');
    await composer.click();
    await composer.fill('Hello test reply');
    await composer.press('Enter');
    await page.waitForTimeout(100);

    expect(await composer.inputValue()).toBe('');
  });

  test('Resolve hides composer and shows Resolved state banner', async ({ page }) => {
    await openPending(page);
    await page.getByRole('button', { name: /^accept$/i }).click();
    await page.waitForTimeout(400);

    // Resolve is in the MessageComposer action bar — use exact text to avoid matching the "Resolved" tab
    await page.locator('button:not([role="tab"])', { hasText: 'Resolve' }).first().click();
    await page.waitForTimeout(400);

    // Composer gone
    await expect(page.locator('textarea[aria-label="Compose message"]')).not.toBeVisible();

    // StateBanner Resolved variant has "Resolved" text span
    // Its container: border-t border-border bg-card (from StateBanner component)
    await expect(
      page.locator('div.border-t.border-border.bg-card span', { hasText: 'Resolved' }),
    ).toBeVisible();
  });
});

// ─── Search and filter ────────────────────────────────────────────────────────

test.describe('Search and filter', () => {
  test('typing filters the conversation list', async ({ page }) => {
    await gotoInbox(page);

    const countBefore = await listItems(page).count();

    await inboxSearch(page).fill('Fatema');
    await page.waitForTimeout(200);

    const countAfter = await listItems(page).count();
    expect(countAfter).toBeLessThanOrEqual(countBefore);
    expect(countAfter).toBeGreaterThan(0);
  });

  test('non-matching query shows zero results', async ({ page }) => {
    await gotoInbox(page);

    await inboxSearch(page).fill('xyznotexistanywhere');
    await page.waitForTimeout(200);

    expect(await listItems(page).count()).toBe(0);
  });

  test('Clear search button resets the list', async ({ page }) => {
    await gotoInbox(page);

    await inboxSearch(page).fill('Fatema');
    await page.waitForTimeout(200);

    const clearBtn = page.locator('button[aria-label="Clear search"]').first();
    await expect(clearBtn).toBeVisible();
    await clearBtn.click();
    await page.waitForTimeout(200);

    expect(await inboxSearch(page).inputValue()).toBe('');
    expect(await listItems(page).count()).toBeGreaterThan(0);
  });

  test('AI tab filters list to AI-handled conversations', async ({ page }) => {
    await gotoInbox(page);

    const allCount = await listItems(page).count();

    // Tab label is "AI 5" (includes count) — match by prefix
    await page.locator('[role="tab"]', { hasText: /^AI/ }).first().click();
    await page.waitForTimeout(200);

    const aiCount = await listItems(page).count();
    expect(aiCount).toBeLessThanOrEqual(allCount);
  });

  test('Pending tab shows pending conversations', async ({ page }) => {
    await gotoInbox(page);

    await page.locator('[role="tab"]', { hasText: /^Pending/ }).first().click();
    await page.waitForTimeout(200);

    expect(await listItems(page).count()).toBeGreaterThan(0);
  });
});

// ─── Info sidebar ─────────────────────────────────────────────────────────────

test.describe('Info sidebar', () => {
  test('Info button opens and closes contact details panel', async ({ page }) => {
    await gotoInbox(page);
    await openFirst(page);

    // Starts closed
    await expect(page.locator('aside p', { hasText: 'Details' })).not.toBeVisible();

    // Open
    await page.locator('button[aria-label="Show details"]').click();
    await page.waitForTimeout(200);
    await expect(page.locator('aside p', { hasText: 'Details' })).toBeVisible();

    // Close
    await page.locator('button[aria-label="Hide details"]').click();
    await page.waitForTimeout(200);
    await expect(page.locator('aside p', { hasText: 'Details' })).not.toBeVisible();
  });
});
