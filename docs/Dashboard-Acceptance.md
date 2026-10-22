# Batch Dashboard 1.4.1 Acceptance

## Scope

The dashboard is a read-only overview of the current ASC import, not scientific quality control. It does not delete files, remove outliers, modify spectra, alter analysis inclusion or change the original XLSX contract. No parsing success rate is shown.

Counts and wavelength coverage use the entire batch regardless of preview selection or search. Failure categories open original filenames and parser reasons. Narrow windows use a separate movable overview; wider windows use the plot sidebar. New imports cancel and invalidate old summaries.

## Verified

- Platform build and Windows PowerShell 5.1 build/package, with compiler warnings treated as errors.
- Existing scientific, export, boundary and WPF regression tests; new summary, category, independent-selection and stale-batch tests.
- Private reference checks include valid spectra and intentionally added parser-failure examples; source files remain unchanged. Detailed measurements and source identities stay in the private local acceptance record.
- Original 78-file endpoint regression using a private isolated copy selected from the historical manifest. The test contract was not relaxed.
- Synthetic 100-file x 3101-point regression. Approximately 0.4–0.46 seconds measures parsing only, not a complete user workflow.
- Rendered 1280x720 and 1920x1080 layouts at logical 100%, 125% and 150% scaling, plus empty, all-failed and loading states. Selected renders and the narrow overview were visually inspected.
- Per-user clean installation, upgrade from 1.3.1, repeated setup, installed payload verification/launch and temporary uninstall.
- Local 1.4.1 installer, portable ZIP and synthetic demo checksums.

## Limits

Physical alternate-DPI, multimonitor, other laboratory computers and independent user trials remain unverified. No new full-cell workbook comparison is claimed for this display-only increment; existing raw-export regressions remain passing. GitHub release availability must be verified separately after CI completes. Public demonstrations must use the clearly labeled synthetic files.
