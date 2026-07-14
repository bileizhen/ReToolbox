# Third-party notices and provenance review

ReToolbox references or integrates with third-party projects. This document is informational and is not legal advice.

## Atlas Toolbox

- Project: https://github.com/Atlas-OS/atlas-toolbox
- Upstream license observed: GPL-3.0
- Use in ReToolbox: design and product reference; the README previously described ReToolbox as “based on” Atlas Toolbox.
- Required release action: maintainers must review Git history and source similarity to identify any copied or adapted implementation. Any GPL-covered derivative material must retain the notices, source availability, and licensing obligations required by its license. The repository’s Apache-2.0 declaration does not override third-party obligations.

## Previously integrated remote tools

The following remote execution paths are disabled because ReToolbox does not currently ship independently verified pinned hashes or a trusted publisher policy:

- Microsoft Activation Scripts / MAS Chinese translation
- EdgeRemover
- Windows Defender Remover
- Direct GitHub release installers such as mpv_PlayKit

These projects retain their own copyrights and licenses. Re-enabling any integration requires recording an immutable upstream version, official source URL, expected SHA-256 digest or trusted Authenticode publisher, and applicable license notice.

## Windows Package Manager

Software installation uses Microsoft Windows Package Manager (`winget`). Individual installed applications are governed by their respective publishers and licenses.
