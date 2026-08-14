const { test, expect, chromium } = require("playwright/test");

test("REIGN /reign live booking flow", async () => {
  const apiBase = "http://localhost:5012";
  const webUrl = "http://localhost:5000/reign";
  const browser = await chromium.launch({
    headless: true,
    executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const result = { checks: {}, details: {} };

  await page.goto(webUrl, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "REIGN AI" })).toBeVisible();
  result.checks.heading = true;

  await expect(page.locator("#booking-phone")).toBeVisible();
  await expect(page.locator("#booking-time")).toBeVisible();
  await expect(page.locator("#booking-notes")).toBeVisible();
  await expect(page.getByRole("button", { name: "Book Appointment" })).toBeVisible();
  result.checks.bookingControls = true;

  await expect(page.locator("#sms-phone")).toBeVisible();
  await expect(page.locator("#sms-message")).toBeVisible();
  await expect(page.getByRole("button", { name: "Send" })).toBeVisible();
  result.checks.smsControls = true;

  await page.waitForFunction(
    () => document.querySelectorAll("#booking-service option").length >= 4,
    null,
    { timeout: 10000 }
  );

  const options = await page.locator("#booking-service option").evaluateAll((nodes) =>
    nodes.map((n) => n.textContent.trim())
  );
  result.details.options = options;
  expect(options).toContain("QV - $150 - Less than 30 minutes");
  expect(options).toContain("HH - $300 - 30 minutes");
  expect(options).toContain("HR - $500 - 60 minutes");
  result.checks.serviceDisplay = true;

  const phone = "5550100811ui";
  await page.locator("#booking-phone").fill(phone);
  await page.locator("#booking-service").selectOption("33333333-3333-3333-3333-333333333333");
  await page.locator("#booking-time").fill("2026-08-15T12:45");
  await page.locator("#booking-notes").fill("Browser UI verification booking");
  await page.getByRole("button", { name: "Book Appointment" }).click();

  await expect(page.locator(".alert-success")).toContainText("HR");
  await expect(page.locator(".alert-success")).toContainText("$500");
  await expect(page.locator(".alert-success")).toContainText("Pending");
  result.details.confirmation = await page.locator(".alert-success").innerText();
  result.checks.confirmation = true;

  await page.locator("#sms-message").fill("hello from browser verification");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.locator("body")).toContainText("Thanks hello from browser verification");
  result.checks.smsReply = true;

  const appointments = await (await fetch(`${apiBase}/api/appointments`)).json();
  const found = appointments.find(
    (a) =>
      a.phone === phone &&
      a.service === "HR" &&
      a.price === 500 &&
      a.durationMinutes === 60 &&
      a.status === "Pending"
  );
  expect(found).toBeTruthy();
  result.details.apiAppointment = found;
  result.checks.databaseResult = true;

  await page.screenshot({ path: ".openclaw/reign-live-verification.png", fullPage: true });
  await browser.close();
  console.log(`REIGN_LIVE_RESULT ${JSON.stringify(result)}`);
});
