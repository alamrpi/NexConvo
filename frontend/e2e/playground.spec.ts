import { test, expect, type Page } from '@playwright/test';

const PASSWORD = 'Password123';

function uniqueSlug(): string {
  return `pw-${Date.now().toString(36)}-${Math.floor(Math.random() * 1e4)}`;
}

async function signupFresh(page: Page): Promise<{ slug: string; email: string }> {
  page.on('console', msg => console.log('BROWSER CONSOLE:', msg.text()));
  page.on('pageerror', err => console.log('BROWSER ERROR:', err.message));
  page.on('requestfailed', request => console.log('REQUEST FAILED:', request.url(), request.failure()?.errorText));
  page.on('response', response => console.log('RESPONSE:', response.url(), response.status()));

  const slug = uniqueSlug();
  const email = `owner@${slug}.test`;
  await page.goto('/signup');
  await page.waitForLoadState('networkidle');
  await page.getByLabel('Company name').fill('PW Co');
  await page.getByLabel('Workspace URL').fill(slug);
  await page.getByLabel('Your name').fill('PW User');
  await page.getByLabel('Work email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Create workspace' }).click();
  await expect(page).toHaveURL(/\/dashboard/, { timeout: 60_000 });
  return { slug, email };
}

test.describe('Chat Playground', () => {
  test.setTimeout(120_000); // Allow extra time for Next.js compilation
  test('loads providers and models dynamically and sends a message', async ({ page }) => {
    // 1. Sign up and navigate to the playground
    await signupFresh(page);
    await page.goto('/dashboard/chat/playground');

    // 2. Wait for the page structure to be visible
    await expect(page.getByRole('heading', { name: 'Playground' })).toBeVisible();

    // 3. The provider dropdown should be disabled since we only return the active provider
    // The SelectTrigger for Provider has aria-label="Model Configuration provider"
    const providerDropdown = page.getByRole('combobox', { name: 'Model Configuration provider' });
    await expect(providerDropdown).toBeDisabled({ timeout: 15000 }); // give it time to load from BFF

    // 4. Check if models are loaded into the model dropdown
    // The SelectTrigger for Model has aria-label="Model Configuration model"
    const modelDropdown = page.getByRole('combobox', { name: 'Model Configuration model' });
    await expect(modelDropdown).toBeEnabled();

    // Click to see models
    await modelDropdown.click();
    
    // Select GPT-4o Mini directly
    await page.getByRole('option', { name: 'GPT-4o Mini' }).click();

    // 5. Send a chat message (verifies handleKeyDown is fixed)
    const chatInput = page.getByLabel('Message input');
    await chatInput.fill('Hello AI, are you there?');
    
    // Hit Enter to send
    await chatInput.press('Enter');

    // The message should appear in the chat stream
    // Look for a div containing our text
    await expect(page.getByText('Hello AI, are you there?')).toBeVisible();

    // Since we're in E2E, we might actually get an AI response back from the mock or real provider,
    // but verifying our message is enough to ensure handleKeyDown worked and didn't crash.
  });
});
