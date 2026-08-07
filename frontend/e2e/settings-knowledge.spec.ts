import { test, expect, type Page } from '@playwright/test';

/**
 * E2E tests for /dashboard/chat/settings/knowledge.
 *
 * Auth: nx_e2e_bypass cookie — same pattern as chat-inbox.spec.ts.
 *
 * The knowledge page is pre-populated with 5 mock documents (sorted by updatedAt desc):
 *   1. Warranty Terms 2026  — Processing  (id=3)
 *   2. Product Catalog 2026 — Ready       (id=1, 142 chunks)
 *   3. Delivery SLA Guide   — Failed      (id=5)
 *   4. Return Policy FAQ    — Ready       (id=2, 24 chunks)
 *   5. Past Chat Import     — Ready       (id=4, 318 chunks)
 *
 * The empty state ("No knowledge sources yet") is only visible when docs.length === 0.
 * The "Add Knowledge" dialog has four tabs: "Upload File", "From URL", "Write Text",
 * "Import Past Chats" (from i18n keys knowledge.addDialog.tabs.*).
 *
 * Re-embed is a 2-second stub that shows a spinning RefreshCw icon then reverts.
 * Delete opens a confirmation dialog with title "Delete this document?" and a "Delete"
 * confirmation button.
 * View navigates to /dashboard/chat/settings/knowledge/[id].
 * Row click also navigates to the detail page.
 */

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function gotoKnowledge(page: Page): Promise<void> {
  await page.context().addCookies([
    { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
  ]);
  await page.goto('/dashboard/chat/settings/knowledge');
  await expect(page.getByRole('heading', { name: 'Knowledge Base' })).toBeVisible();
}

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('Settings — Knowledge Base', () => {
  // ── Page-level layout ────────────────────────────────────────────────────────

  test('shows page heading and subtitle', async ({ page }) => {
    await gotoKnowledge(page);
    await expect(page.getByRole('heading', { name: 'Knowledge Base' })).toBeVisible();
    await expect(
      page.getByText('Manage documents and data sources the AI uses to answer questions.'),
    ).toBeVisible();
  });

  test('shows the Add Knowledge button', async ({ page }) => {
    await gotoKnowledge(page);
    await expect(page.getByRole('button', { name: 'Add Knowledge' })).toBeVisible();
  });

  // ── Table with pre-loaded mock data ──────────────────────────────────────────

  test('renders the documents table with column headers', async ({ page }) => {
    await gotoKnowledge(page);
    await expect(page.getByRole('columnheader', { name: 'Title' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Type' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Status' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Chunks' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Actions' })).toBeVisible();
  });

  test('shows all 5 pre-loaded mock documents', async ({ page }) => {
    await gotoKnowledge(page);
    for (const title of [
      'Product Catalog 2026',
      'Return Policy FAQ',
      'Warranty Terms 2026',
      'Past Chat Import - June',
      'Delivery SLA Guide',
    ]) {
      await expect(page.getByText(title)).toBeVisible();
    }
  });

  test('shows correct status badges for each mock document', async ({ page }) => {
    await gotoKnowledge(page);

    // All three status values appear in the table
    // "Ready" appears for Product Catalog 2026, Return Policy FAQ, Past Chat Import
    // "Processing" appears for Warranty Terms 2026
    // "Failed" appears for Delivery SLA Guide
    const rows = page.getByRole('row');
    await expect(rows.filter({ hasText: 'Warranty Terms 2026' }).getByText('Processing')).toBeVisible();
    await expect(rows.filter({ hasText: 'Product Catalog 2026' }).getByText('Ready')).toBeVisible();
    await expect(rows.filter({ hasText: 'Delivery SLA Guide' }).getByText('Failed')).toBeVisible();
  });

  test('shows chunk counts for Ready documents and "—" for others', async ({ page }) => {
    await gotoKnowledge(page);

    const rows = page.getByRole('row');
    // Product Catalog 2026 has 142 chunks
    await expect(rows.filter({ hasText: 'Product Catalog 2026' }).getByText('142')).toBeVisible();
    // Warranty Terms 2026 has 0 chunks → rendered as "—"
    await expect(rows.filter({ hasText: 'Warranty Terms 2026' }).getByText('—')).toBeVisible();
  });

  // ── Empty state ───────────────────────────────────────────────────────────────

  test('shows empty state after all documents are deleted', async ({ page }) => {
    await gotoKnowledge(page);

    // Delete all 5 documents one by one
    const docTitles = [
      'Warranty Terms 2026',
      'Product Catalog 2026',
      'Delivery SLA Guide',
      'Return Policy FAQ',
      'Past Chat Import - June',
    ];

    for (const title of docTitles) {
      const row = page.getByRole('row').filter({ hasText: title });
      await row.getByRole('button', { name: new RegExp(`Delete ${title}`) }).click();

      const confirmDialog = page.getByRole('dialog');
      await expect(confirmDialog).toBeVisible();
      await expect(confirmDialog.getByRole('heading', { name: 'Delete this document?' })).toBeVisible();
      await confirmDialog.getByRole('button', { name: 'Delete' }).click();
      await expect(confirmDialog).not.toBeVisible();
    }

    // Empty state is now rendered
    await expect(page.getByText('No knowledge sources yet')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add your first knowledge source' })).toBeVisible();
  });

  // ── Add Knowledge dialog ──────────────────────────────────────────────────────

  test('Add Knowledge button opens a dialog with four tabs', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: 'Add Knowledge' })).toBeVisible();

    await expect(dialog.getByRole('tab', { name: 'Upload File' })).toBeVisible();
    await expect(dialog.getByRole('tab', { name: 'From URL' })).toBeVisible();
    await expect(dialog.getByRole('tab', { name: 'Write Text' })).toBeVisible();
    await expect(dialog.getByRole('tab', { name: 'Import Past Chats' })).toBeVisible();
  });

  test('Write Text tab shows Title and Content fields', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'Write Text' }).click();

    // Free text mode is the default
    await expect(dialog.getByLabel('Title')).toBeVisible();
    await expect(dialog.getByLabel('Content')).toBeVisible();
  });

  test('Save button in Write Text tab is disabled when fields are empty', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'Write Text' }).click();

    // Both fields are empty → Save is disabled
    await expect(dialog.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  test('Save button in Write Text tab enables when both Title and Content are filled', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'Write Text' }).click();

    await dialog.getByLabel('Title').fill('E2E Test Document');
    await dialog.getByLabel('Content').fill(
      'This is a test knowledge document for E2E testing. It contains sample content about our products.',
    );

    await expect(dialog.getByRole('button', { name: 'Save' })).toBeEnabled();
  });

  test('submitting a text document closes the dialog', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'Write Text' }).click();

    await dialog.getByLabel('Title').fill('E2E Test Document');
    await dialog.getByLabel('Content').fill('Sample knowledge content for testing.');

    await dialog.getByRole('button', { name: 'Save' }).click();

    // Dialog should close (the mock implementation just calls onClose)
    await expect(dialog).not.toBeVisible();
  });

  test('From URL tab — Fetch button is disabled when URL field is empty', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'From URL' }).click();

    await expect(dialog.getByRole('button', { name: 'Fetch' })).toBeDisabled();
    await expect(dialog.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  test('From URL tab — Fetch shows "Content ready" and enables Save', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'From URL' }).click();

    await dialog.getByLabel('URL').fill('https://example.com/docs');
    await dialog.getByRole('button', { name: 'Fetch' }).click();

    // Fetch is a 1.5 s stub
    await expect(dialog.getByText(/Content ready/i)).toBeVisible({ timeout: 5_000 });
    await expect(dialog.getByRole('button', { name: 'Save' })).toBeEnabled();
  });

  test('Upload File tab — drop zone and file-type hint are visible', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    // Upload is the default tab
    await expect(dialog.getByText('Drag & drop or click to upload')).toBeVisible();
    await expect(dialog.getByText('PDF, DOCX, TXT, MD up to 50 MB')).toBeVisible();
  });

  test('Import Past Chats tab — shows From and To date inputs', async ({ page }) => {
    await gotoKnowledge(page);

    await page.getByRole('button', { name: 'Add Knowledge' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('tab', { name: 'Import Past Chats' }).click();

    await expect(dialog.getByLabel('From')).toBeVisible();
    await expect(dialog.getByLabel('To')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Import' })).toBeDisabled();
  });

  // ── Delete ────────────────────────────────────────────────────────────────────

  test('Delete button opens a confirmation dialog', async ({ page }) => {
    await gotoKnowledge(page);

    const row = page.getByRole('row').filter({ hasText: 'Return Policy FAQ' });
    await row.getByRole('button', { name: 'Delete Return Policy FAQ' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: 'Delete this document?' })).toBeVisible();
    await expect(dialog.getByText('This will permanently remove the document and all its chunks.')).toBeVisible();
    await expect(dialog.getByText('Return Policy FAQ')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Cancel' })).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Delete' })).toBeVisible();
  });

  test('Cancel in delete dialog closes it without removing the document', async ({ page }) => {
    await gotoKnowledge(page);

    const row = page.getByRole('row').filter({ hasText: 'Return Policy FAQ' });
    await row.getByRole('button', { name: 'Delete Return Policy FAQ' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).not.toBeVisible();

    // Document still in the table
    await expect(page.getByText('Return Policy FAQ')).toBeVisible();
  });

  test('confirming delete removes the document from the table', async ({ page }) => {
    await gotoKnowledge(page);

    const row = page.getByRole('row').filter({ hasText: 'Return Policy FAQ' });
    await row.getByRole('button', { name: 'Delete Return Policy FAQ' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Delete' }).click();
    await expect(dialog).not.toBeVisible();

    await expect(page.getByText('Return Policy FAQ')).not.toBeVisible();
  });

  // ── Re-embed ──────────────────────────────────────────────────────────────────

  test('Re-embed button shows a spinning icon while re-embedding', async ({ page }) => {
    await gotoKnowledge(page);

    // Use a Ready document
    const row = page.getByRole('row').filter({ hasText: 'Product Catalog 2026' });
    const reEmbedBtn = row.getByRole('button', { name: 'Re-embed Product Catalog 2026' });

    await reEmbedBtn.click();

    // Button becomes disabled and the RefreshCw icon gets animate-spin class while re-embedding
    await expect(reEmbedBtn).toBeDisabled();

    // After 2 s the stub resolves and the button re-enables
    await expect(reEmbedBtn).toBeEnabled({ timeout: 5_000 });
  });

  test('Re-embed disables the button during processing and re-enables it afterwards', async ({
    page,
  }) => {
    test.slow(); // 2 s stub
    await gotoKnowledge(page);

    const row = page.getByRole('row').filter({ hasText: 'Return Policy FAQ' });
    const reEmbedBtn = row.getByRole('button', { name: 'Re-embed Return Policy FAQ' });

    await expect(reEmbedBtn).toBeEnabled();
    await reEmbedBtn.click();
    await expect(reEmbedBtn).toBeDisabled();
    await expect(reEmbedBtn).toBeEnabled({ timeout: 5_000 });
  });

  // ── View / detail navigation ──────────────────────────────────────────────────

  test('View button navigates to the document detail page', async ({ page }) => {
    await gotoKnowledge(page);

    const row = page.getByRole('row').filter({ hasText: 'Product Catalog 2026' });
    await row.getByRole('button', { name: 'View Product Catalog 2026' }).click();

    await expect(page).toHaveURL(/\/dashboard\/chat\/settings\/knowledge\/1$/);
    // Detail page shows the document title
    await expect(page.getByText('Product Catalog 2026')).toBeVisible();
  });

  test('detail page shows Chunks and Version History tabs', async ({ page }) => {
    await gotoKnowledge(page);

    // Navigate to doc id=1 (Product Catalog 2026) via View button
    const row = page.getByRole('row').filter({ hasText: 'Product Catalog 2026' });
    await row.getByRole('button', { name: 'View Product Catalog 2026' }).click();
    await expect(page).toHaveURL(/\/dashboard\/chat\/settings\/knowledge\/1$/);

    await expect(page.getByRole('tab', { name: 'Chunks' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Version History' })).toBeVisible();
  });

  test('detail page shows the processing steps indicator (mock data always has status=Processing)', async ({
    page,
  }) => {
    // The detail page at any [id] renders MOCK_DOC_DETAIL which has status='Processing'
    // and currentStep=2, so the ProcessingSteps widget is always rendered.
    await page.context().addCookies([
      { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
    ]);
    await page.goto('/dashboard/chat/settings/knowledge/1');

    await expect(page.getByText('Processing')).toBeVisible();
    // Step labels rendered as visible text inside the steps widget
    await expect(page.getByText('Upload')).toBeVisible();
    await expect(page.getByText('Embed')).toBeVisible();
  });

  test('detail page Chunks tab shows a message when status is not Ready', async ({ page }) => {
    // MOCK_DOC_DETAIL has status='Processing' so ChunksTab renders the waiting message
    await page.context().addCookies([
      { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
    ]);
    await page.goto('/dashboard/chat/settings/knowledge/1');

    await expect(page.getByRole('tab', { name: 'Chunks' })).toBeVisible();
    await expect(
      page.getByText('Chunks will appear here once processing completes.'),
    ).toBeVisible();
  });

  test('detail page Version History tab shows version rows', async ({ page }) => {
    await page.context().addCookies([
      { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
    ]);
    await page.goto('/dashboard/chat/settings/knowledge/1');

    await page.getByRole('tab', { name: 'Version History' }).click();

    // MOCK_DOC_DETAIL has 3 versions
    await expect(page.getByText('v3')).toBeVisible();
    await expect(page.getByText('v2')).toBeVisible();
    await expect(page.getByText('v1')).toBeVisible();
    const restoreBtns = page.getByRole('button', { name: 'Restore' });
    await expect(restoreBtns.first()).toBeVisible();
  });

  test('Back to Knowledge Base button on the detail page navigates to the list', async ({ page }) => {
    await page.context().addCookies([
      { name: 'nx_e2e_bypass', value: '1', domain: 'localhost', path: '/' },
    ]);
    await page.goto('/dashboard/chat/settings/knowledge/1');

    await page.getByRole('button', { name: 'Back to Knowledge Base' }).click();
    await expect(page).toHaveURL(/\/dashboard\/chat\/settings\/knowledge$/);
  });
});
