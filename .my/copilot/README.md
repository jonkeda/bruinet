# Bruinet review prompts

Prompts for getting a serious architectural and product review of Bruinet from VS Code Copilot Chat, Claude Code
or GPT-5, not a superficial "looks good". Bruinet's README is `README.md`.

## How to use

- `00-full-review.md` is the whole review in one prompt: one long answer covering every topic.
- `01`–`09` are the same review split by topic. Each one stands alone, so run them in separate chats for deeper
  answers, or one after another in the same chat.
- Every prompt says where the README is. In Copilot Chat, attach it with `#file:README.md`;
  elsewhere, paste it where the prompt says.

## The idea to have challenged

A reviewer will likely focus less on the page objects and more on the chain **same scenario → service → headless
MAUI → real device**. Most frameworks pick one layer. Bruinet treats the execution layer as a deployment target for
the test rather than a different test altogether. That is the idea worth seeing challenged: `03-multiple-heads.md`
goes after it directly.

## Files

| File | Topic |
| --- | --- |
| `prompt.md` | The original draft the prompts came from |
| `00-full-review.md` | All nine topics in one prompt |
| `answers/` | One reviewer's answer per prompt, and `00-summary.md` across them |
| `01-technical-differentiation.md` | How unusual the combination is |
| `02-maui-headless.md` | The MAUI headless runner |
| `03-multiple-heads.md` | One scenario, many execution heads |
| `04-market-position.md` | Which category, which positioning |
| `05-enterprise-value.md` | What enterprises would pay for, ranked |
| `06-blind-spots.md` | Missing capabilities |
| `07-open-source-viability.md` | Adoption as open source |
| `08-commercial-potential.md` | Bruinet as a company |
| `09-one-sentence.md` | "What is Bruinet really?" |
