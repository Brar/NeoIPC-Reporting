# Changelog

Notable changes to the NeoIPC reporting service and the container image it ships in.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the version lives in
[Directory.Build.props](Directory.Build.props). The release workflow reads the section matching the
released version out of this file and publishes it as the GitHub Release body, so a release cannot be
cut for a version this file does not describe.

Report content is not authored here: the Quarto sources and the R package are pinned by
`pinned-sources.yml` and baked into the image at build time, so a change to a report appears in that
product's own changelog, and here only as the pin that carries it.

## [0.2.0] - 2026-07-06

### Added

- Report rendering through Quarto with an archival-PDF toolchain, and the source-generated parameter
  schemas the DHIS2 app reads to build its forms.

### Changed

- Immutable upstream pins for the report sources and the R package, so a published image records
  exactly which versions of each it was built from and can be rebuilt to the same bytes.
