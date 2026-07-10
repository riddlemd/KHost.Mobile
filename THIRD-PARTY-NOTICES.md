# Third-Party Notices

KHost.Mobile (KHost Cue) itself is licensed under the PolyForm Shield License
1.0.0 (see [`LICENSE`](LICENSE)). It incorporates and/or distributes the
third-party components listed below, each of which remains under its own license
and copyright. All of them are permissive (MIT / Apache-2.0) and permit inclusion
in a source-available and/or commercial product; nothing here changes the terms
of the PolyForm Shield License covering KHost.Mobile's own code.

> The full verbatim text of the Apache License 2.0 (the only bundled license that
> requires its text to travel with binary distributions) is included under
> [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt). The MIT License is
> reproduced inline in [§3](#3-common-license-texts).

---

## 1. Components distributed with KHost.Mobile

These ship inside a KHost.Mobile build (the compiled app and/or its bundled
browser assets and fonts).

| Component | Used in | License | Copyright / Author |
|---|---|---|---|
| Microsoft.Maui.Controls (.NET MAUI) | app shell | MIT | Microsoft / .NET Foundation |
| Microsoft.AspNetCore.Components.WebView.Maui (Blazor Hybrid) | app shell | MIT | Microsoft / .NET Foundation |
| Microsoft.AspNetCore.SignalR.Client | client library | MIT | Microsoft / .NET Foundation |
| Microsoft.Extensions.Http | client library | MIT | Microsoft / .NET Foundation |
| Microsoft.Extensions.Logging.Debug | app shell (debug logging) | MIT | Microsoft / .NET Foundation |
| Microsoft.Extensions.* (transitive: DI, Logging, Options, Primitives, …) | app shell, client | MIT | Microsoft / .NET Foundation |
| Bootstrap 5.3.3 (`wwwroot/lib/bootstrap/**`) | web UI assets | MIT | The Bootstrap Authors |
| Open Sans — `OpenSans-Regular.ttf` (from the .NET MAUI template) | app font | Apache-2.0 | Digitized data © 2010–2011 Google; design Ascender Corp. |

License texts:
- **MIT** is reproduced in [§3](#3-common-license-texts).
- **Apache-2.0**: [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt)
  (https://www.apache.org/licenses/LICENSE-2.0). Retain the Open Sans attribution
  ("Digitized data copyright 2010–2011, Google Corporation") with the font file.

There are **no copyleft (GPL/LGPL) components** and no components that must be
kept replaceable; every distributed dependency is permissively licensed.

---

## 2. Build, test, and tooling dependencies (NOT distributed)

The following are used only to build, test, or develop KHost.Mobile and are
**not** shipped in any KHost.Mobile build, so they impose no distribution
obligations: xUnit (Apache-2.0), xunit.runner.visualstudio (Apache-2.0), and
Microsoft.NET.Test.Sdk (MIT).

---

## 3. Common license texts

### The MIT License

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The full text of the Apache License 2.0 is bundled under
[`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt) and is included with binary
distributions.

---

*This file is informational, reflects KHost.Mobile's dependencies as of this
revision, and is not legal advice. Verify license identifiers for any
revenue-bearing release.*
