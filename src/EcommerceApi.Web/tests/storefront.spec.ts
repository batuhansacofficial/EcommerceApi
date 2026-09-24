import { test, expect } from '@playwright/test';

test('real API: register, cart, checkout, restore session, order history, logout', async ({ page, context }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Shop the collection', exact: true }).click();
  await page.getByRole('searchbox').fill('Overshirt');
  await expect(page.getByRole('button', { name: 'Add The Relaxed Overshirt to bag', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Account', exact: true }).first().click();
  await page.getByRole('button', { name: 'New here? Create an account' }).click();
  await page.getByLabel('First name').fill('Browser');
  await page.getByLabel('Last name').fill('Test');
  await page.getByLabel('Email', { exact: true }).fill(`e2e-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('E2E-browser-password-2026!');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByText('Welcome, Browser.', { exact: true })).toBeVisible();
  expect((await context.cookies()).find(cookie => cookie.name.includes('mira-session'))?.httpOnly).toBe(true);
  await page.getByRole('button', { name: 'Add The Relaxed Overshirt to bag', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Shopping bag' })).toBeVisible();
  await page.getByRole('button', { name: 'Increase The Relaxed Overshirt', exact: true }).click();
  await expect(page.locator('.quantity > span')).toHaveText('2');
  await page.getByRole('button', { name: 'Create simulated order' }).click();
  await expect(page.locator('.order-card')).toHaveCount(1);
  await expect(page.getByText('The Relaxed Overshirt × 2', { exact: true })).toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: 'Account', exact: true }).first().click();
  await expect(page.locator('.order-card')).toHaveCount(1);
  await expect(page.getByText('Pending', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page.locator('.order-card')).toHaveCount(0);
  expect((await page.request.get('/api/auth/me')).status()).toBe(401);
});

test('catalog search and mobile layout use the real catalog', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByRole('button', { name: 'Shop the collection', exact: true }).click();
  await page.getByRole('searchbox').fill('Overshirt');
  await expect(page.locator('.catalog-page .product-card')).toHaveCount(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/real-api-mobile.png', fullPage: true });
});
