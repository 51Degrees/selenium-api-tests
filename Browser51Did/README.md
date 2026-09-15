# 51Did browser acceptance tests

Category `Browser51Did`. Fifteen tests proving, in real Chrome and Firefox,
that a 51Did is created only after every other piece of data is complete and
only from an answer the visitor actually gave, with the 51Degrees Preference
Management Platform (PMP) and the client script on a publisher's page.

They drive a **demo**, a small web app that serves every page the tests
load. The dotnet demo is `Examples/Cloud/pmp-web` in
[device-detection-dotnet-examples](https://github.com/51Degrees/device-detection-dotnet-examples).
The pages, the recorder that watches them, the change watcher and the stub
consent platform are plain static files in that demo, so a demo in another
language copies them, fills the same placeholders, and these same tests then
prove it behaves the same.

## What they prove

1. A choice made on one site is read on another, the visitor is not asked
   again, and a 51Did is still created with the signal source recorded as
   direct. Chrome and Firefox, because the choice travels on a third party
   cookie.
2. A page with a consent platform and no PMP sends the framework string,
   the cloud decodes it, and the 51Did records that the usage was decoded.
3. The common path. The first request carries no answer and creates
   nothing, the visitor answers, the second card follows at once, exactly
   one further request carries the answer and every snippet result, and the
   51Did comes back. Both tag orders.
4. A change of answer on one page makes a new 51Did with the sequence one
   higher and tells a change handler.
5. A change of answer carried to the next page in the same tab.
6. The alternative answer creates a `non-marketing` 51Did with the direct
   flag, fires the publisher's action, and offers no second card.
7. Whether the regulation applies, read from the client script. This cannot
   pass until the IsGdpr property is released in the cloud, and reports
   inconclusive with that reason until then.
8. A page with nowhere to get an answer from creates nothing, says so once,
   and stores a record carrying no answer.
9. A page carrying the PMP and no client script tag gets the script added by
   the PMP from the cloud that served it.
10. `data-object-name` names the object everywhere, and its absence gives
    the default `fod`.

Every assertion is on an ordering of requests and page states, never on a
clock. The guard, `Harness.RequireTheNewClientScript`, fails every test in a
class unless the client script the pages load carries the new template's
user prompt block.

## The pages

| Route | Markup | Tests |
| --- | --- | --- |
| `common` | PMP, then client script | common path, shared choice (as `site-a.localtest` then `site-b.localtest`), alternative, both IsGdpr |
| `common-script-first` | client script, then PMP | common path, other tag order |
| `change` | PMP, client script, change watcher | change of answer on one page |
| `two/one`, `two/two` | the same as `change` | change of answer across pages |
| `consent` | stub consent platform, then client script | consent platform only |
| `no-platform` | client script alone | no PMP at all |
| `platform-only` | PMP alone | PMP adds the client script, default object name |
| `named-object` | PMP with `data-object-name="fiftyOneData"` | object name attribute |

Each route is served under a mode prefix. `/cloud/` pages load the client
script and the PMP straight from the cloud, and the client script posts to
the cloud's `/api/v4/json`. `/pipeline/` pages have the demo's own pipeline
serve the client script, which posts to the path the demo's descriptor names
in `ExampleDescriptor.JsonEndpointPath`. The tests read that path from the
mode rather than assuming one.

## Running them

The demo is started once for the run through `Examples/ExampleApps.cs`, as
the Contract tests start an example.

| Variable | Meaning |
| --- | --- |
| `51DEGREES_CLOUD_ENDPOINT` | The cloud, including its `api/v4` path. Handed to the demo unchanged. |
| `51DEGREES_RESOURCE_KEY` | A resource key the cloud creates 51Dids for standard and personalized answers with. Handed to the demo under this name, and never printed, because every message a test produces has it taken out. |
| `_51DEGREES_RESOURCE_KEY_51DID` | Read only where `51DEGREES_RESOURCE_KEY` is unset, and handed to the demo as `51DEGREES_RESOURCE_KEY`. This is the name continuous integration sets, for a resource key carrying the 51Did product. |
| `DEMO_LANG` | The demo to launch from the sibling checkout. `dotnet` unless set. |
| `DEMO_URL` | A demo already running, which is used instead of launching one. |
| `DEMO_MODE` | `cloud` unless set to `pipeline`. |
| `CLOUD_ROOT_URL` | Not used by these tests, but the suite's assembly set up requires it. |

```bash
env CLOUD_ROOT_URL="http://localhost:5050/" \
    51DEGREES_CLOUD_ENDPOINT="http://localhost:5050/api/v4/" \
    51DEGREES_RESOURCE_KEY="<a key that creates 51Dids>" \
    dotnet test --filter TestCategory=Browser51Did
```

`env` is used because bash cannot export a variable whose name starts with a
digit, which is also why the name continuous integration sets starts with an
underscore. In PowerShell write `${env:51DEGREES_RESOURCE_KEY} = '...'`.

With the endpoint or both resource key names unset every test reports
inconclusive with the reason. A resource key that cannot create reports inconclusive with the
cloud's own refusal, and so does a cloud that will not hold a shared choice.
The shared choice needs the browser to keep the cloud's `Secure`
`SameSite=None` cookie, which both browsers did from plain HTTP on
`localhost` when measured, so it skips only for a plain HTTP cloud reached by
another name.

## Adding a demo in another language

1. Copy the demo's static files and templates, and fill the same
   placeholders from the same two variables.
2. Serve every route under `/cloud/`, and under `/pipeline/` once the
   language's own web integration serves the client script, answering any
   host name, because the tests load the pages as `site-a.localtest` and
   `site-b.localtest` on the demo's port.
3. Add an entry to `ExampleApps.Demos` saying how to launch it.
4. Run the category with `DEMO_LANG` set to that entry.

## What the pipeline mode proves today

The dotnet demo serves every route under `/pipeline/` as well, and
`DEMO_MODE=pipeline` drives those pages. Their client script comes from the
demo's own pipeline, which renders the user prompt block only with the
pipeline packages that carry the new template, so with the released
packages those pages show no prompt block and the tests that press it fail
there. Two things differ in that mode. The PMP recognises a client script
tag only by the cloud's path, `/api/v4/<name>.js`, so the demo's pipeline
pages carry a tag that is not `async`, which has always run before the PMP
looks for its object. And on a page with no client script tag the PMP adds
the cloud's script, which posts to the cloud's `/api/v4/json`, whilst
`Visitor.ClientRequests` looks on the pipeline's path, so
`NoClientScriptTag_PmpAddsItAndTheAnswerStillCreates` fails in that
mode until the suite looks for the added script's own requests.
