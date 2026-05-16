# Third-Party Notices

This package redistributes binaries and source-derived bindings produced from the following projects.

## bgfx

- Repository: https://github.com/bkaradzic/bgfx
- License: BSD-2-Clause
- Copyright (c) 2010-2024 Branimir Karadzic

```
Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

   1. Redistributions of source code must retain the above copyright notice, this
      list of conditions and the following disclaimer.

   2. Redistributions in binary form must reproduce the above copyright notice,
      this list of conditions and the following disclaimer in the documentation
      and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY COPYRIGHT HOLDER ``AS IS'' AND ANY EXPRESS OR IMPLIED
WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL COPYRIGHT
HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## bx

- Repository: https://github.com/bkaradzic/bx
- License: BSD-2-Clause
- Copyright (c) 2010-2024 Branimir Karadzic

(Same BSD-2-Clause terms as bgfx above.)

## bimg

- Repository: https://github.com/bkaradzic/bimg
- License: BSD-2-Clause
- Copyright (c) 2010-2024 Branimir Karadzic

(Same BSD-2-Clause terms as bgfx above.)

## 3rd-party code linked into bgfx-shared-lib

bgfx, bx, and bimg bundle additional 3rd-party libraries under `3rdparty/` directories in
their respective repositories. A license audit of those folders is performed on every
upstream bump; the consolidated list of licenses for code that ends up linked into
`bgfx-shared-lib` (and the tools `shaderc`, `texturec`, `geometryc`) is reproduced below.

> TODO: Populate this section by running `build/audit-3rdparty-licenses.ps1` after the
> first successful native build. The audit traverses `external/{bgfx,bx,bimg}/3rdparty/`
> and collects every LICENSE/COPYING file.
