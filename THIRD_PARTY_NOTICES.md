# Third-party notices and provenance review

ReToolbox references or integrates with third-party projects. This document is informational and is not legal advice.

## Atlas Toolbox

- Project: https://github.com/Atlas-OS/atlas-toolbox
- Upstream license observed: GPL-3.0
- Use in ReToolbox: design and product reference; the README previously described ReToolbox as “based on” Atlas Toolbox.
- Required release action: maintainers must review Git history and source similarity to identify any copied or adapted implementation. Any GPL-covered derivative material must retain the notices, source availability, and licensing obligations required by its license. The repository’s Apache-2.0 declaration does not override third-party obligations.

## Verified remote tools

Administrator-level third-party tools are downloaded only from immutable upstream revisions and are checked against the recorded size and SHA-256 digest before execution:

- Microsoft Activation Scripts 3.11, commit `b9906472628468de9f6e53b00cf5b06c318e8b96`, `MAS_AIO.cmd` SHA-256 `a0a6f670c9eb25468e9d41c9c2fc511b310250b31b43d02ef7c5694532dbba95`.
- EdgeRemover v1.9.5, commit `17220aca63d55d0d210d98004a504cbbcf25cb63`, `RemoveEdge.ps1` SHA-256 `ca33fe16a9c6baf54b27d18864928fcb62b41886164606bd8dc6cda008d4b168`. Upstream license: The Unlicense.
- Windows Defender Remover `release13-rev1`, source archive SHA-256 `c88881a0ebfe49fea282cf97f8409d8ba16501b4d74bb44d1ccf6df525190313`.

These projects retain their own copyrights and licenses. Updating an integration requires recording the new immutable upstream version, official source URL, expected SHA-256 digest or trusted Authenticode publisher, and applicable license notice.

## Windows Package Manager

Software installation, including the supported `shinchiro.mpv` package, uses Microsoft Windows Package Manager (`winget`). Individual installed applications are governed by their respective publishers and licenses. The former direct `mpv_PlayKit` release integration is not shipped.
