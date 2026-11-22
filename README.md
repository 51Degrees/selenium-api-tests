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
| `Browser51Did` | The 51Did user prompt work in Chrome and Firefox, driving a demo's pages. See [Browser51Did/README.md](Browser51Did/README.md). | Cloud CI when something they prove changed (vs `:8080`, dotnet demo). |
| `ModuleJavaScript` | A web server module's JavaScript example, in Chrome and Firefox. The page at `EXAMPLE_URL` loads the JavaScript the module serves, which writes the browser's screen width into the `51D_ScreenPixelsWidth` cookie, and then shows in the element `widthvalue` the width the module detected from that cookie. Both must be the browser's own screen width, so a page that shows the width the data file holds for the User-Agent fails. | The NGINX and Varnish module CIs, each against its own example served with the module running. |
| `Browser` | That a browser starts at all and runs the script on a page served from the test process. No cloud, no key, no example. Also runs the `ModuleJavaScript` check against a stand-in module served the same way, to show it passes on a working page and fails on a broken one. | This repository's own CI, on every runner image it uses. |

Select a subset with `--filter TestCategory=Contract`,
`--filter TestCategory=CloudInternal` or
`--filter TestCategory=ModuleJavaScript`.

## How a run is wired

- **Example app** - for CI the example is launched by the caller and its URL is
  passed in `EXAMPLE_URL`. For local runs set `EXAMPLE_LANG` (e.g. `dotnet`) and the
  suite launches the example from the sibling checkout.
- **Cloud endpoint** - `CLOUD_ROOT_URL` is the cloud an example launched here is
  pointed at, and the cloud the `CloudInternal` tests hit directly. An example
  that is already running was pointed at its data by whoever started it, so with
  `EXAMPLE_URL` set the suite needs neither `CLOUD_ROOT_URL` nor
  `PAID_RESOURCE_KEY`, which is what lets an on-premise example run `Contract`.
- **Browser** - tests get their browsers from `Helpers/BrowserDrivers.cs`. A
  driver the machine already provides is used first, named by
  `CHROMEWEBDRIVER`, `GECKOWEBDRIVER` or `EDGEWEBDRIVER` or found on the path.
  When the machine provides none, Selenium Manager fetches one. Set
  `SELENIUM_URL` to drive a browser on a Selenium grid instead. Chrome and
  Firefox are each started once, before the first test that asks
  `BrowserDrivers` for them, and every Chrome, Edge and Firefox session, that
  first start included, gives each command up to 300 seconds, because a start
  on a fresh or busy runner can take longer than the driver's default of 60
  seconds. A session on a Selenium grid gets the same 300 seconds. The first
  start's time is written to that test's output. `BrowserDrivers.Prepare`
  runs every browser headless and accepting insecure certificates, and gives
  every Chrome and Edge `--no-sandbox` and `--disable-dev-shm-usage`, so a test
  sets none of these itself. The Chrome, Edge and Firefox drivers start
  quietly, with no start up banner, no verbose log and no console window. The
  51Did tests are the exception, as `Browser51Did/Harness.cs` starts their
  Chrome and Firefox itself, with the same preparation, time limit and quiet
  drivers, but leaves Selenium Manager to find or fetch the driver and makes
  no first start.

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
| `CHROMEWEBDRIVER` / `GECKOWEBDRIVER` / `EDGEWEBDRIVER` | optional | A driver, or the directory holding one. GitHub's Linux, Windows and macOS runner images set these. A repository that runs this suite leaves them to it and does not read them itself. |
| `CHROME_BIN` / `FIREFOX_BIN` / `EDGE_BIN` | optional | The browser binary to drive, when it is not on the path. |
| `EXAMPLE_URL` / `EXAMPLE_LANG` | `Contract` | The example app to test (CI / local). |
| `EXAMPLE_URL` | `ModuleJavaScript` | The module's JavaScript example page, served with the module running. There is no example this suite could start in its place, so the tests fail without it. |
| `_51DEGREES_CLOUD_ENDPOINT` / `_51DEGREES_RESOURCE_KEY` | `Browser51Did` | The cloud and the resource key. Handed to the demo under each name it reads, which for the dotnet demo are these and the older names. |
| `_51DEGREES_RESOURCE_KEY_51DID` | `Browser51Did` | Read where `_51DEGREES_RESOURCE_KEY` is unset. The name CI sets, for a resource key carrying the 51Did product. |
| `51DEGREES_CLOUD_ENDPOINT` / `51DEGREES_RESOURCE_KEY` | `Browser51Did` | The older names, still read after every current name and reported as deprecated. |
| `DEMO_URL` / `DEMO_LANG` / `DEMO_MODE` | `Browser51Did` | The demo to test, and `cloud` or `pipeline` pages. |

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

ModuleJavaScript against a web server module's JavaScript example, served with
the module running on a data file that holds ScreenPixelsWidthJavascript, which
the free Lite data file does not:

```bash
export EXAMPLE_URL="http://localhost:8889/index.html"
dotnet test --filter TestCategory=ModuleJavaScript
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
- **The NGINX and Varnish module CIs** check this repo out as a sibling, serve
  their JavaScript example with the module running, and run `ModuleJavaScript`
  against it. They find and start no browser or driver of their own.
