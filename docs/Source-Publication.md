# Public Source Publication

## Purpose and scope

Publish a buildable, privacy-reviewed FluoLab 1.3.1 source snapshot for competition review. The destination is <https://github.com/heyinghao8038-collab/FluoLab>. This is source publication, not a new scientific release or a claim of independently validated research results.

## Tasks

1. Copy only the reviewed source/build/documentation allowlist into an isolated publication tree.
2. Remove private reference measurements and absolute personal paths from that copy. Keep the original local reference suite unchanged.
3. Include generated, explicitly labeled synthetic ASC examples and the empty mouse seed only.
4. Scan all publication files and Git entries for excluded paths, credentials, private measurements and unintended binaries.
5. Build and run the public synthetic/scientific and WPF test suites from the publication tree.
6. Compare production source fingerprints with the original workspace.
7. Push a reviewed main-branch commit through normal Git authentication, without force push.
8. Verify public files using an unauthenticated reader and record the result locally.

## Exclusions

Real spectra and animal records, private reference assertions, local workspace memory, source-machine usernames/paths, work-project drafts, cached translations, user PDFs, credentials, model/runtime archives, generated executables, private QA outputs, and old distribution archives are excluded.

The original release and laboratory data stay local. GitHub publication does not add network functionality to the application.

## Reproducibility and limitations

The public test runner uses synthetic data. The private 78-file fixture is deliberately not distributed. The normal .NET build uses the checked-in mouse assets and does not require Node or Python. Rebuilding those assets requires the documented development dependencies. Optional translation requires a separately obtained, digest-matched CPU component pack; that pack is not part of the source repository.

No new source license is selected by this task. Existing third-party notices and licenses remain included. Physical alternate-DPI, other-PC, WPS and human end-to-end timing checks are not newly claimed.

## Publication result — 2026-10-19

The reviewed 91-file snapshot is published on main at commit `29c1350b828e7595b8845a43129c1cdeb1b0b42f`. Unauthenticated GitHub API reads confirm a public repository and the expected file tree. A clone with credential helpers disabled succeeds. The public preparation tree passes the Windows build, synthetic/scientific/performance suite and WPF controls tests; optional real inference is skipped without the separate CPU pack. The original laboratory workspace and private reference suite are preserved.
