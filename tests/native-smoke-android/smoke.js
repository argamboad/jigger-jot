// Native smoke — Android (NATIVE-7, ADR-018). Mirrors tests/E2E.Tests/NativeSmokeTests.cs
// (the Windows leg) but in Node: Android WebView's CDP doesn't support the browser-context
// management Playwright's ConnectOverCDPAsync needs, so this leg uses playwright-core's
// _android module instead (adb + WebView attach — the purpose-built path, Node-only).
//
// Prereqs (the CI job or a local rehearsal provides them): an emulator/device with the DEBUG
// app installed and launched (EmbedAssembliesIntoApk=true — a fast-deployment APK won't start
// from a plain `adb install`), `adb reverse tcp:5438 tcp:5438`, the API on
// http://localhost:5438, Mailpit on MAILPIT_BASE_URL (default http://localhost:8027).
const { _android } = require('playwright-core');

const PKG = process.env.NATIVE_SMOKE_PKG || 'com.jiggerjot.app';
const MAILPIT = process.env.MAILPIT_BASE_URL || 'http://localhost:8027';

async function mailpit(path, init) {
  const res = await fetch(`${MAILPIT}${path}`, init);
  if (!res.ok) throw new Error(`Mailpit ${path} -> ${res.status}`);
  return res;
}

// Same matching rules as the C# Mailpit helper: OTP subject + a standalone 6-digit code.
async function waitForOtp(toEmail, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const list = await (await mailpit('/api/v1/messages?limit=50')).json();
    const summary = (list.messages ?? []).find(m =>
      (m.To ?? []).some(a => a.Address?.toLowerCase() === toEmail.toLowerCase()) &&
      (m.Subject ?? '').toLowerCase().includes('verification code'));
    if (summary) {
      const detail = await (await mailpit(`/api/v1/message/${summary.ID}`)).json();
      const match = `${detail.Text} ${detail.HTML}`.match(/(?<!\d)(\d{6})(?!\d)/);
      if (match) return match[1];
    }
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error(`No OTP email for ${toEmail} within ${timeoutMs / 1000}s`);
}

// Boot: attaching to the app's WebView proves the app process started (a G7-style crash dies
// here), and the visible login box proves Blazor booted inside it.
async function bootToLogin(device, attempt) {
  const webView = await device.webView({ pkg: PKG }, { timeout: 60_000 });
  const page = await webView.page();
  console.log(`connected (attempt ${attempt}): ${page.url()}`);

  // Unhandled .NET exceptions in the Blazor WebView only show up as console errors; without
  // this the smoke just times out waiting for UI and the root cause lives in logcat noise.
  page.on('console', msg => {
    if (msg.type() === 'error' || msg.type() === 'warning') console.error(`[webview ${msg.type()}] ${msg.text()}`);
  });
  page.on('pageerror', e => console.error(`[webview pageerror] ${e.message}`));

  const emailBox = page.getByTestId('login-email');
  await emailBox.waitFor({ state: 'visible', timeout: 60_000 });
  return { page, emailBox };
}

// The WHOLE journey is retried once, not just the boot wait. MAUI's BlazorWebView is torn down and
// re-created when Android recreates the Activity early in startup ("Cannot access a disposed
// object: 'IServiceProvider'" at WebViewManager.AttachToPageAsync; first seen on run 32769356890,
// where the same APK passed twice that morning) — the WebView the smoke attached to then goes away
// underneath it. Any failure BEFORE the login box is visible still retries, as it always did. Until
// 2026-09-15 only the boot wait retried, so the race was
// survivable ONLY while it landed before the login box rendered; when it lands after (run
// 34983821740: "connected", login box seen, then `fill` found no element 30 s later) the run failed
// on a healthy app. A journey-wide retry covers both landings; a real startup fault (the G7 class
// this canary exists for) still fails BOTH attempts.
const ATTEMPTS = 2;

// The shapes the teardown takes, none of which an app fault produces twice in a row: the attached
// target is gone, its execution context died, or the DOM the smoke was mid-way through is empty.
function looksLikeWebViewReplaced(message) {
  return [
    'Target page, context or browser has been closed',
    'Target closed',
    'Execution context was destroyed',
    "waiting for getByTestId('login-email')",
  ].some(s => message.includes(s));
}

async function journey(device, attempt, seen) {
  const { page, emailBox } = await bootToLogin(device, attempt);
  seen.page = page;
  seen.booted = true;

  // OTP sign-in end-to-end through the real API + Mailpit (the native body-token transport).
  // The address is per-ATTEMPT: a retry must not read the first attempt's code back out of Mailpit.
  const email = `native-smoke-${Date.now()}@example.com`;
  await mailpit('/api/v1/messages', { method: 'DELETE' });
  await emailBox.fill(email);
  await page.getByTestId('login-send-otp').click({ timeout: 60_000 });
  const code = await waitForOtp(email, 60_000);
  await page.getByTestId('login-otp-code').fill(code);
  // Both buttons are disabled while the page is busy (disabled="@_busy"); on a cold emulator the
  // send round-trip + Blazor re-render can outlast Playwright's default 30 s click wait even though
  // the OTP mail is already in Mailpit (run 34005419602: "waiting for element to be … enabled" on
  // Verify code, green on re-run). Give the clicks the same 60 s every other wait here already has.
  await page.getByTestId('login-verify-otp').click({ timeout: 60_000 });
  // Attached, not visible: the responsive header collapses sign-out behind the hamburger on
  // a phone-sized window (same reasoning as the Windows leg).
  await page.getByTestId('sign-out').first().waitFor({ state: 'attached', timeout: 60_000 });

  // One authorized page: Household loads its data — proves the native Bearer path.
  await page.goto('https://0.0.0.1/household');
  await page.getByTestId('household-rename-input').waitFor({ state: 'visible', timeout: 60_000 });
  const members = await page.getByTestId('member-row').count();
  if (members !== 1) throw new Error(`expected 1 roster row for a fresh owner, saw ${members}`);
  return page;
}

// What the page looked like when it failed — a closed target and a live-but-empty one are different
// diagnoses, and CI's `adb logcat -d` after the fact has come back empty (run 34983821740).
async function describeFailure(page) {
  if (!page) return 'no page was attached';
  if (page.isClosed()) return 'the attached WebView target was CLOSED';
  try {
    const seen = await page.evaluate(() => ({ url: location.href, chars: document.body.innerText.trim().length }));
    return `the page is open at ${seen.url} with ${seen.chars} characters of text`;
  } catch (e) {
    return `the page is open but unreachable: ${e.message.split('\n')[0]}`;
  }
}

(async () => {
  const devices = await _android.devices();
  if (devices.length === 0) throw new Error('no adb device/emulator attached');
  const device = devices[0];
  console.log(`device: ${device.serial()}`);

  for (let attempt = 1; ; attempt++) {
    let failed;
    const seen = {};
    try {
      await journey(device, attempt, seen);
      break;
    } catch (e) {
      failed = e;
    }
    const message = failed.message ?? String(failed);
    console.error(`attempt ${attempt}: ${await describeFailure(seen.page)}`);
    if (attempt >= ATTEMPTS || (seen.booted && !looksLikeWebViewReplaced(message))) throw failed;
    console.error(`attempt ${attempt} failed (${message.split('\n')[0]}); force-stopping and relaunching once (MAUI WebView re-created)`);
    await device.shell(`am force-stop ${PKG}`);
    await new Promise(r => setTimeout(r, 2000));
    await device.shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`);
  }

  console.log('native smoke (android): boot + OTP sign-in + household roster OK');
  await device.close();
  process.exit(0);
})().catch(e => { console.error(`native smoke (android) FAILED: ${e.message}`); process.exit(1); });
