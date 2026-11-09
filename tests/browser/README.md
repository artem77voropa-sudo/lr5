# Browser regression for LR05

Run only against the local synthetic Development database. Required: Python 3.10+, `pip install playwright`, installed Google Chrome, API on http://localhost:5080 and probe servers on localhost:4173 and localhost:4174 serving `demonstrations/cors-probe`.

Configure `ConnectionStrings__SecureLab` and `SeedUsers__Password` privately for the API; supply the same `SeedUsers__Password` environment variable to this runner. Do not put credentials in this directory. Seed the dedicated database before the run. No password/cookie values are printed or saved.

From the repository root:

```powershell
python tests/browser/lab05-browser-checks.py --stage fixed
```

`before` verifies expected vulnerable behavior against the isolated local scaffold snapshot. `no-credentials` verifies the brief negative CORS control after temporarily removing AllowCredentials; restore the original policy and rerun `fixed` afterward. These modes do not modify API code themselves.

The runner uses an actual Chrome browser, performs login through the UI, checks both DOM sinks, legitimate text, access controls, simple/preflight/cookie CORS, CSP and response headers. The fixed-mode bypass_csp context is confined to the test browser and proves safe DOM behavior independently of CSP. It does not weaken server configuration.

Outputs go to `docs/evidence/lr05`. Each mode overwrites its own prior protocol/screenshots. Assertions stop a failed run; a partial JSON file is not proof of success. Created incident fixtures are deleted after observation; seed fixtures remain. The before-mode PASS means the intentionally unsafe baseline behaved as predicted, not that it was secure.

Synthetic data only. Screenshots of the login page are taken after clearing the password field. Network records include only a boolean cookiePresent, never the cookie value.
