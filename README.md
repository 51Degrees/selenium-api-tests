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
  `SELENIUM_URL` to drive a browser on a Selenium grid instead. CloakBrowser
  is never started by the suite, which attaches to one already running in a
  container when `CLOAK_DEBUGGER_ADDRESS` names it. See
  [Running under CloakBrowser](#running-under-cloakbrowser).

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
| `CLOAK_DEBUGGER_ADDRESS` | optional | Host and port of a running CloakBrowser to attach to, e.g. `127.0.0.1:9222`. The Cloak tests are skipped when it is unset. |
| `CLOAKWEBDRIVER` | optional | A chromedriver of the same major version as CloakBrowser, or the directory holding one. Selenium Manager fetches one when it is unset. |
| `EXAMPLE_URL` / `EXAMPLE_LANG` | `Contract` | The example app to test (CI / local). |
| `51DEGREES_CLOUD_ENDPOINT` / `51DEGREES_RESOURCE_KEY` | `Browser51Did` | Handed to the demo under these names, the ones every language's demo reads first. |
| `_51DEGREES_RESOURCE_KEY_51DID` | `Browser51Did` | Read where `51DEGREES_RESOURCE_KEY` is unset. The name CI sets, for a resource key carrying the 51Did product. |
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

CloudInternal against a cloud you control:

```bash
export CLOUD_ROOT_URL="http://localhost:8080/"
export FREE_RESOURCE_KEY="<free key>"
export PAID_RESOURCE_KEY="<paid key>"
export ENTERPRISE_V4_LICENSE="<license>"
dotnet test --filter TestCategory=CloudInternal
```

## Running under CloakBrowser

[CloakBrowser](https://github.com/CloakHQ/CloakBrowser) is a patched Chromium
that its vendor publishes for browser automation. Visitors arrive in browsers
like it as well as in Chrome and Firefox, so the tests of the examples can be
run in it too.

The tests that use it are the two whose names start with `Cloak_` in
`Browser`, the `CloakTests` class in `CloudInternal`, and the tests whose
names end in `_Cloak` in `Contract`. They are skipped, with a reason that
names the variable, until `CLOAK_DEBUGGER_ADDRESS` is set, so a run that does
not set it is unchanged.

### Starting the browser

The CloakBrowser binary is closed source, so the suite never downloads it and
never starts it. It runs only inside the vendor's Docker image, and the suite
attaches a driver to it through the DevTools port the container serves. Pin
the image by digest, so that the browser cannot change under a tag. Mount
nothing from the machine into the container and pass it no keys.

On a Linux machine, such as a CI runner, use host networking, so that the
browser reaches the example and the pages the tests serve on `localhost`.

```bash
docker run -d --name cloak --network host \
  -e CLOAKBROWSER_AUTO_UPDATE=false \
  cloakhq/cloakbrowser:0.5.12@sha256:2fdd1289154f30594c7bab74943ea7baa6b617a9ec026ab4bf339c2ebabcb712 \
  cloakserve
export CLOAK_DEBUGGER_ADDRESS="127.0.0.1:9222"
```

Host networking opens the DevTools port on every network interface of the
machine, and whoever can reach that port controls the browser. Use it only on
a machine that takes no connections from outside, which a hosted CI runner is.

With Docker Desktop on Windows, publish the port on the loopback address
instead, and tell the browser that `localhost` is the machine Docker Desktop
runs on.

```bash
docker run -d --name cloak -p 127.0.0.1:9222:9222 \
  -e CLOAKBROWSER_AUTO_UPDATE=false \
  cloakhq/cloakbrowser:0.5.12@sha256:2fdd1289154f30594c7bab74943ea7baa6b617a9ec026ab4bf339c2ebabcb712 \
  cloakserve "--host-resolver-rules=MAP localhost host.docker.internal"
export CLOAK_DEBUGGER_ADDRESS="127.0.0.1:9222"
```

`CLOAKBROWSER_AUTO_UPDATE=false` stops the container looking for a newer
browser when it starts, so the browser that runs is the one in the pinned
image. Remove the container with `docker rm -f cloak` when the run is over.

### The driver

The driver is ChromeDriver, and it has to be of the same major version as the
Chromium inside CloakBrowser, which is 146 in the image above and is not the
version of the Chrome on the machine. The suite asks the browser for its
version and has Selenium Manager fetch that driver, so there is nothing to
install, and a chromedriver on the path is never used. Set `CLOAKWEBDRIVER`
to name a driver where Selenium Manager cannot fetch one.

Selenium Manager also downloads a Chrome of that version when the machine has
none, and nothing runs it. Set `SE_AVOID_BROWSER_DOWNLOAD=true` to stop that.

### What is different for a test

The browser was started by the container and not by the driver, which changes
three things.

- Arguments in the options, such as `--headless` or `--user-agent`, never
  reach the browser. It sends its own user agent, and it runs with a window on
  the container's virtual display.
- ChromeDriver refuses mobile emulation for a browser it attaches to. The
  `_Cloak` tests of the client-side overrides therefore emulate no device, and
  work with the screen size the browser reports for itself.
- The browser outlives the test. Each attach moves to a new tab, closes the
  others and clears the cookies and the cache, so a test starts as it would in
  a browser started for it.

### Keys the browser is given

CloakBrowser is a third party's closed source browser, so consider which keys
it is shown. The `Contract` tests put no key in anything they give the
browser and pass on what the example serves, so an example that keeps its
resource key on the server gives CloakBrowser no key. The `CloakTests` class
in `CloudInternal` is different, because it runs the cloud's JavaScript in the
browser, and that script carries the resource key and the license it was
requested with. A pipeline that runs `CloudInternal` under CloakBrowser should
therefore use keys it is content for that browser to see.

### License

The browser binary has its own license, which is separate from the MIT
license of the vendor's wrapper code. The image above carries the vendor's
free build under version 1.3 of the CloakBrowser Binary License, dated July
2026. [Read that version](https://github.com/CloakHQ/CloakBrowser/blob/2e379e402b52838d37e469a40e6195e887e314d1/BINARY-LICENSE.md)
before running it. The suite uses no account and no license key.

## This repository's own CI

The "Build and test" workflow builds the suite on every push and pull request,
and runs it in three jobs. The first runs the tests that need no browser, no
cloud and no keys. The second starts Chrome and Firefox on `ubuntu-latest`,
`ubuntu-22.04-arm` and `ubuntu-24.04-arm`, and prints what each runner
provides before it does. The third starts CloakBrowser from its pinned image
on `ubuntu-latest` and runs the `Browser` category with a driver attached to
it. Together they prove a change here before any language repository picks it
up.

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
- **Either** adds CloakBrowser by starting its container before the tests and
  setting `CLOAK_DEBUGGER_ADDRESS`, as
  [Running under CloakBrowser](#running-under-cloakbrowser) describes.
