# Third-party notices

This unofficial Unity package includes prebuilt native plugins at:

- `Runtime/Plugins/Windows/x86_64/ZXing.dll`
- `Runtime/Plugins/Android/arm64-v8a/libZXing.so`

Those binaries contain the components and notices listed below. The notices are informational and do not change the components' respective license terms.

## ZXing-C++

- Project: [zxing-cpp/zxing-cpp](https://github.com/zxing-cpp/zxing-cpp)
- Version: [v3.1.0](https://github.com/zxing-cpp/zxing-cpp/releases/tag/v3.1.0)
- Pinned commit: [`885baaf0840335153c1a487fa65f9c1388702c81`](https://github.com/zxing-cpp/zxing-cpp/commit/885baaf0840335153c1a487fa65f9c1388702c81)
- License: Apache License 2.0

The native build uses the upstream ZXing-C++ source without source patches and enables its C API, readers, and QR Code support. ZXing-C++ is developed by the ZXing-C++ project and its contributors. Its complete license is included in [ZXing-C++ LICENSE.md](ZXing-C++%20LICENSE.md).

## libzueci

ZXing-C++ includes libzueci in its reader core. It is compiled into both native plugins.

Source notice: [`core/src/libzueci/zueci.c`](https://github.com/zxing-cpp/zxing-cpp/blob/885baaf0840335153c1a487fa65f9c1388702c81/core/src/libzueci/zueci.c)

Copyright (C) 2022 gitlost

All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

## Bjoern Hoehrmann UTF-8 decoder DFA

libzueci contains a UTF-8 decoder derived from [Bjoern Hoehrmann's UTF-8 decoder DFA](https://bjoern.hoehrmann.de/utf-8/decoder/dfa/).

Copyright (c) 2008-2009 Bjoern Hoehrmann <bjoern@hoehrmann.de>

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

## LLVM C++ runtime (Android plugin only)

The bundled Android plugin statically links `libc++_static` and `libc++abi` from Android NDK r27c (`27.2.12479018`), Clang 18.0.3, based on LLVM revision [`d8003a456d14a3deb8054cdaa529ffbf02d9b262`](https://github.com/llvm/llvm-project/commit/d8003a456d14a3deb8054cdaa529ffbf02d9b262).

The LLVM Project is licensed under Apache License 2.0 with LLVM Exceptions. The Apache License 2.0 terms are included in [LICENSE.md](LICENSE.md). The applicable LLVM exception is reproduced below:

> As an exception, if, as a result of your compiling your source code, portions of this Software are embedded into an Object form of such source code, you may redistribute such embedded portions in such Object form without complying with the conditions of Sections 4(a), 4(b) and 4(d) of the License.
>
> In addition, if you combine or link compiled forms of this Software with software that is licensed under the GPLv2 ("Combined Software") and if a court of competent jurisdiction determines that the patent provision (Section 3), the indemnity provision (Section 9) or other Section of the License conflicts with the conditions of the GPLv2, you may retroactively and prospectively choose to deem waived or otherwise exclude such Section(s) of the License, but only in their entirety and only with respect to the Combined Software.

Legacy LLVM portions are covered by the following notice:

Copyright (c) 2003-2019 University of Illinois at Urbana-Champaign. All rights reserved.

Developed by the LLVM Team, University of Illinois at Urbana-Champaign, <http://llvm.org>.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal with the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

- Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimers.
- Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimers in the documentation and/or other materials provided with the distribution.
- Neither the names of the LLVM Team, University of Illinois at Urbana-Champaign, nor the names of its contributors may be used to endorse or promote products derived from this Software without specific prior written permission.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE CONTRIBUTORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS WITH THE SOFTWARE.

## Microsoft Visual C++ runtime (Windows plugin only)

The bundled Windows plugin is built with the Microsoft Visual C++ runtime linked statically. Microsoft runtime components remain subject to the applicable Microsoft Visual Studio or Build Tools license terms. See Microsoft's [Visual C++ redistribution documentation](https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files).
