# Exact Online in the Recorder: "You cannot access this right now"

Status: **solved by way A** (2026-11-24). The analysis below is as it was written from one screenshot.

**What was done:** the Recorder got `--as os` (Windows): its webview signs in with the account the person is signed
in to Windows with. Opened that way on `https://start.exactonline.nl/`, it came through the company's sign-in to
Exact Online's menu (`/docs/MenuPortal.aspx`), where without it the sign-in ended on the blocked page. So the cause
was the first of section 2: the webview did not show the device. The error code was never read, so which rule it was
is still not known by name. One more thing showed on the way and was fixed: the Recorder reopened the last address it
was on, also when that was a step of the sign-in (`AADSTS900561`, "the endpoint only accepts POST requests"); it now
remembers only an address of the app itself.

| | |
| --- | --- |
| What happened | Signing in to `https://start.exactonline.nl/` in the Recorder with an Exact Software work account ends on a Microsoft page: "You cannot access this right now. Your sign-in was successful but does not meet the criteria to access this resource. For example, you might be signing in from a browser, app, or location that is restricted by your admin." |
| The short answer | It is not a firewall. The password and the second factor were accepted; a **Conditional Access** rule of Exact's Microsoft Entra tenant then refused the sign-in because of *where it came from*: most likely the Recorder's webview does not show Microsoft that it runs on a managed, compliant company device |
| What decides it | The error code under **More details** on that page. Section 3 |
| What not to do | Get round the rule (pretend to be another browser, copy a session from Edge). Section 5 |

## 1. What the message means

The page is Microsoft's, not Exact Online's. Reading it part by part:

| It says | So |
| --- | --- |
| "Your sign-in was successful" | The account, the password and the second factor are fine. Nothing is wrong with the account |
| "does not meet the criteria to access this resource" | After the sign-in, a rule was checked and failed. Such rules are Conditional Access policies: an admin sets them per application (here: Exact Online) |
| "a browser, app, or location that is restricted by your admin" | The three usual things a rule looks at: the program that signs in, the device it runs on, and the network it comes from |

A firewall would have looked different: the page would not load at all, or time out, or a proxy's own block page
would show. Here the sign-in pages loaded, answered, and Microsoft itself said no.

## 2. What it can be, most likely first

| # | Cause | How likely | Why |
| --- | --- | --- | --- |
| 1 | **The device is not recognised as managed or compliant.** The rule asks for a company device that Intune reports as compliant (or that is joined to the company's directory). The laptop is one, but the Recorder's webview does not say so | Most likely | A browser proves the device with the Windows account the person is signed in to the laptop with. Edge does that by itself, Chrome with the Microsoft sign-in support switched on. An embedded WebView2 does **not**, unless the app that hosts it asks for it (section 4). To Microsoft the Recorder looks like an unknown device that happens to know the password |
| 2 | **The program is not an approved one.** The rule allows named browsers or apps only | Possible | A webview identifies itself as Edge's engine, so a rule on the browser's name alone would usually pass. A rule that asks for an "approved client app" would not |
| 3 | **The location.** The rule allows the office network or the VPN only | Less likely | The same account, from the same laptop on the same network, would then also fail in Edge. Easy to check: section 3 |
| 4 | **The account may not open this application at all**, or only a test tenant of it | Less likely | The message would more often say that the account has no access. Also checked by trying Edge |
| 5 | **Sign-in risk.** A new, unknown "device" signing in as this account looked suspicious | Unlikely, a side effect of 1 | It goes away with cause 1 |

Why 1 leads: the Recorder keeps its own browser profile (`%LOCALAPPDATA%\Bruinet\Recorder\profiles\own`), on
purpose, so a sign-in is not shared with anything else. That profile has no link to the Windows account, so it
carries no proof of the device.

## 3. How to find out which

Three checks, a few minutes together. None changes anything.

| Check | How | What it tells |
| --- | --- | --- |
| **The code** | On the blocked page, click **More details**. It shows an error code, a request id, a correlation id and a time | `53000`: the device is not compliant. `53001`: the device is not joined to the company's directory. `53003`: blocked by a Conditional Access policy (the general one; the admin can see which). `53004`: more security information must be set up first. `530032` or `530033`: blocked by a security policy on the device or the client. `50158`: an outside security check was not met. The request id and the time are what IT needs to look the sign-in up |
| **Edge** | Open `https://start.exactonline.nl/` in Edge on the same laptop, same network, same account | Works: the account and the location are fine, the program or the device proof is the matter (causes 1 or 2). Fails the same way: it is the location or the account (3 or 4), and the Recorder is not the issue |
| **The sign-in log** | `https://mysignins.microsoft.com` shows your own recent sign-ins; the failed one says which requirement was not met. IT sees the same in Entra's sign-in logs, with the policy's name | The rule by name. This settles it |

Do not paste the request id or the correlation id into a chat or a tracked file: send them to IT only.

## 4. What can be done

| Way | What it is | Needs | Cost |
| --- | --- | --- | --- |
| **A. Let the webview use the Windows account** | WebView2 has a setting for exactly this: the hosting app allows single sign-on with the account the person is signed in to Windows with (`AllowSingleSignOnUsingOSPrimaryAccount`). The webview then shows the device's identity to Microsoft as Edge does, and a rule on a compliant device can pass. It is the supported way for an app to meet such a rule, not a way round it: the device is checked, and has to pass | A change in the Recorder (Windows): the webview's environment is made with that setting before the first page. To try: about half a day, and it may turn out that MAUI's own webview set-up has to be replaced for it. It only helps for cause 1 | With it on, the Recorder signs in as you without asking, to anything that takes the company account. So it becomes a switch at start (`--as os`), off by default, and the profile per `--as` name stays for other accounts |
| **B. An account the rule does not cover** | A customer-like account: a demo or test administration with its own user name and password, not a company account. This is also what a customer has, so what the Recorder sees is what a customer sees | Someone who can give out such an account and administration | None in code. Probably the best data to record anyway: an employee's view of Exact Online may differ from a customer's |
| **C. Ask IT** | With the code and the request id: which rule blocked it, and whether a tool on a managed laptop can be allowed (an exclusion, or the rule met through A) | The three checks of section 3 first | Time, and the answer may be no |
| **D. Record in a real browser** | The first proposal (`proposal.md` in `../ScraperUI`): Chrome or Edge itself, started by the Scraper, with the watcher script. A real browser with its own company profile passes the device check | Building that variant: the engine is the same (`Bruinet.Scraper.Record`), the page would be a browser instead of a webview. The recording browser must then use a profile that is signed in to the company, which is the opposite of the Scraper's rule that its profile is its own | Days, and a decision about that profile |

Proposed order: do the three checks (ten minutes); if Edge works and the code is `53000` or `53001`, try **A**; in
parallel ask for **B**, which is wanted for its own reasons.

## 5. What not to do

| Not this | Why |
| --- | --- |
| Make the webview claim to be another browser (a changed user agent), or switch off parts of it, until the rule lets it through | That is getting round a security control that others manage. The rule exists to keep the administrations of customers from unknown devices |
| Copy cookies or a session out of Edge into the Recorder | The same, and a session copied out of its browser is exactly what the rule is meant to stop |
| Sign in with someone else's account that happens to pass | The recording would be made under another person's name |
| Put the site behind a proxy that signs in for the Recorder | The same as the first, with more parts |

## 6. Questions

| # | Question | Why it matters |
| --- | --- | --- |
| Q1 | What is the error code under **More details**? | It picks the cause in section 2 |
| Q2 | Does the same account open Exact Online in Edge on this laptop? | It separates the Recorder from the account and the network |
| Q3 | Is recording as an employee what is wanted, or as a customer would see it? | If a customer's view: way **B**, and this block does not need solving |
| Q4 | May the Recorder sign in with the Windows account (way **A**)? | It is convenient and it is a wider door: one switch, off by default |
