# selenium-api-tests

Shared Selenium tests that verify both moving parts of the 51Degrees cloud:

- the **cloud under development** against the stable API examples, and
- each **API under development** against the stable public cloud
  (`https://cloud.51degrees.com/`).

It is **not** a submodule. The cloud repo and each API repo check this repo out as a
sibling directory and run it at their integration-test step.

## Test categories

| Category | What it checks | Where it runs |
|---|---|---|
| `Contract` | An example app serves `51Degrees.core.js`, client-side evidence flows back, and the server-rendered page shows a real detection result. | Cloud CI (per example, vs `:8080`) **and** every API CI (vs the public cloud). |
| `CloudInternal` | Cloud response behaviour through a browser: cache reuse, COEP/CORP headers, third-party cookies, client-side overrides, and the per-browser JS endpoints. | Cloud CI only (vs `:8080`). |
| `Browser` | That a browser starts at all and runs the script on a page served from the test process. No cloud, no key, no example. | This repository's own CI, on every runner image it uses. |

Select a subset with `--filter TestCategory=Contract` or
`--filter TestCategory=CloudInternal`.

## How a run is wired

- **Example app** - for CI the example is launched by the caller and its URL is
  passed in `EXAMPLE_URL`. For local runs set `EXAMPLE_LANG` (e.g. `dotnet`) and the
  suite launches the example from the sibling checkout.
- **Cloud endpoint** - `CLOUD_ROOT_URL` is the cloud an example launched here is
  pointed at, and the cloud the `CloudInternal` tests hit directly. An example
  that is already running was pointed at its data by whoever started it, so with
  `EXAMPLE_URL` set the suite needs neither `CLOUD_ROOT_URL` nor
  `PAID_RESOURCE_KEY`, which is what lets an on-premise example run `Contract`.
- **Browser** - a driver the machine already provides is used first, named by
  `CHROMEWEBDRIVER`, `GECKOWEBDRIVER` or `EDGEWEBDRIVER` or found on the path.
  When the machine provides none, Selenium Manager fetches one. Set
  `SELENIUM_URL` to drive a browser on a Selenium grid instead.

## Configuration

All configuration is read from environment variables - nothing is read from a file,
and no keys are committed.

| Variable | Used by | Notes |
|---|---|---|
| `CLOUD_ROOT_URL` | `CloudInternal`, and `Contract` when the suite launches the example | Base cloud URL, e.g. `https://cloud.51degrees.com/`. |
| `PAID_RESOURCE_KEY` | `CloudInternal`, and `Contract` when the suite launches the example | Resource key used by the tests. |
| `FREE_RESOURCE_KEY` | `CloudInternal` | Free resource key for the JS-endpoint tests. |
| `ENTERPRISE_V4_LICENSE` | `CloudInternal` | License passed to the JS endpoint to unlock paid properties. |
| `SELENIUM_URL` | optional | Selenium grid URL, omit to drive a browser on this machine. |
| `CHROMEWEBDRIVER` / `GECKOWEBDRIVER` / `EDGEWEBDRIVER` | optional | A driver, or the directory holding one. GitHub's Linux runner images set these. |
| `CHROME_BIN` / `FIREFOX_BIN` / `EDGE_BIN` | optional | The browser binary to drive, when it is not on the path. |
| `EXAMPLE_URL` / `EXAMPLE_LANG` | `Contract` | The example app to test (CI / local). |

A missing variable only fails the tests that read it, and the failure names the
variable. Nothing is read for the run as a whole, so a `Contract` run against a
running example (`EXAMPLE_URL`) needs no cloud URL and no key, and there is no
need to invent placeholder values to get a run started.

## Why a test was skipped

A test that cannot run says why and is reported as skipped rather than failed,
for example an example app that renders no device id, or a language with no
descriptor. `dotnet test` prints the name of a skipped test and nothing else,
which reads in a CI log as though everything is fine, so this repository ships
a small logger that prints the reason as well:

```
  Skipped Example_RendersRealDetectionResult, because: ... No example descriptor
  registered for EXAMPLE_LANG='bogus'. Known: dotnet, java, node, python, php, rust.
```

It is in `TestLogger` and it is turned on by `test.runsettings`, which the test
project points at, so a plain `dotnet test` gets it with no extra arguments.

## Running locally

Contract against the public cloud, dotnet example from the sibling checkout:

```bash
export CLOUD_ROOT_URL="https://cloud.51degrees.com/"
export PAID_RESOURCE_KEY="<your paid resource key>"
export EXAMPLE_LANG="dotnet"
dotnet test --filter TestCategory=Contract
```

Contract against an example that is already running, which is how CI calls it
and the only settings it needs:

```bash
export EXAMPLE_URL="http://localhost:8080/"
dotnet test --filter TestCategory=Contract
```

CloudInternal against a cloud you control:

```bash
export CLOUD_ROOT_URL="http://localhost:8080/"
export FREE_RESOURCE_KEY="<free key>"
export PAID_RESOURCE_KEY="<paid key>"
export ENTERPRISE_V4_LICENSE="<license>"
dotnet test --filter TestCategory=CloudInternal
```

## This repository's own CI

The "Build and test" workflow builds the suite on every push and pull request,
and runs it in two jobs. The first runs the tests that need no browser, no
cloud and no keys. The second starts Chrome and Firefox on `ubuntu-latest`,
`ubuntu-22.04-arm` and `ubuntu-24.04-arm`, and prints what each runner
provides before it does. Together they prove a change here before any language
repository picks it up.

## Browsers by architecture

GitHub's ARM64 Linux runner images carry Firefox and geckodriver and set
`GECKOWEBDRIVER`, but no Chrome, no Chromium, no ChromeDriver and no Edge,
and they leave `CHROMEWEBDRIVER` and `EDGEWEBDRIVER` unset. The x64 images
carry all of them.

Chrome still runs on ARM64, because Selenium Manager fetches the Chrome for
Testing `linux-arm64` build and a matching driver. That needs
Selenium.WebDriver 4.49.0 or later, which is the first version whose Selenium
Manager ships an ARM64 Linux build. Earlier versions carry only an x64 one,
under a folder named for Linux with no architecture in the name, so on an
ARM64 runner it is picked, cannot start, and every browser test dies in a few
milliseconds with "Exec format error". Do not downgrade the package.

Edge cannot run on ARM64 Linux at all. Microsoft publishes neither the browser
nor the driver for it, so the Edge tests say so and skip.

## CI integration

- **Cloud CI** checks this repo out as `../selenium-api-tests`, builds it once, runs
  `Contract` per example against the local `:8080` container, and runs `CloudInternal`
  once against `:8080`.
- **Each API CI** checks this repo out, launches its own example, and runs `Contract`
  against the public cloud.
