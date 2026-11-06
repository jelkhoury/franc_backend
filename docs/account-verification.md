# Account verification and PR 2 integration

PR 2's user/admin authorization changes are combined with the chatbot feature on `wadih_dev`.
`generate-token-by-email` is preserved as a fully commented implementation in UserController;
there is no registered route, and the frontend no longer calls it.

## Account flow

- Signup creates an unverified account and sends a six-digit code. Its response contains
  `requiresVerification: true` and no JWT. React goes to the verification screen without logging in.
- `POST /api/users/verify-code?email=...&code=...` consumes an email-verification code once,
  marks the account verified, and returns the normal JWT. React stores that token and signs in.
- Password sign-in for an unverified account returns 403 with `code: account_not_verified` after
  verifying the password. React recognizes this stable code in Production and opens verification.
- Forgot/reset password use a separate challenge purpose. A reset code cannot produce a verification
  login token, and an account-verification code cannot reset a password.
- Already verified accounts cannot use verification resend as a passwordless login endpoint.
- Existing exact email identity/casing rules are retained. No email values are rewritten or combined.

## Challenge limits

Codes have six digits, expire after 10 minutes, and are invalid after five wrong guesses. An account
can receive a new code at most once per 60 seconds, across verification and reset requests. The resend
response remains generic for missing, verified, or throttled accounts. React shows a resend countdown;
the backend enforces the limit independently of the browser.

`Users` stores expiry, purpose, send time, failed attempts, and a version. EF uses the version as a
concurrency token for issuance, failed guesses, consumption and password changes. A request that loses
a race does not issue a JWT or overwrite another request's challenge changes. Successful consumption
and account/password updates are saved together.

## Rollout

1. Apply migration `20261106131854_HardenAccountVerification` with `dotnet ef database update`.
   It adds five columns to `Users` and preserves existing users and application data.
2. Restart/deploy .NET and deploy the updated frontend together: old four-digit forms cannot enter
   the new six-digit codes.
3. Codes issued before the migration have no expiry/purpose metadata and are rejected; request a new code.
4. Check signup → verification → sign-in, password reset, Admin actions, and a user attempting another
   user's records using dedicated test accounts.

The implementation work generates the migration only; it does not apply it to Azure. Existing JWTs
keep their normal expiry; this change does not rotate signing keys or revoke previously issued tokens.

## Verification

Backend tests use an isolated in-memory database, captured email delivery and synthetic JWTs. HTTP tests
start a localhost server for actual authentication/authorization, without launching production hosted
workers or connecting to Azure/SMTP. Frontend tests cover signup without a JWT, verification without
the legacy fallback, six-digit input, production unverified-account errors and restored/logout state.

No JWT claim format, signing secret, event/survey API, or Flask authentication change is required.
