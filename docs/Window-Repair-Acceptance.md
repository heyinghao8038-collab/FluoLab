# FluoLab 1.3.1 Window Repair Acceptance

## Reproduced failure and correction

The real 1.3.0 window had three custom buttons in its visual/accessibility tree but no visible title strip. The symptom persisted in the first native-frame prototype: a desktop capture showed a negative Y origin (-53), because the unchanged 900-DIP default exceeded the scaled monitor work area. Restoring the frame alone was insufficient; startup placement also had to be fixed. The previous detached-content screenshot checks did not detect this. A new native-frame check fails on the old build.

Version 1.3.1 removes late custom chrome/content replacement, restores the Windows-managed caption and resize frame, and fits initial normal taskbar windows to the actual monitor work area/DPI with an 8-DIP margin. Native movement/resizing remains available after startup. The main title includes the release number. Footer functions now have larger blue outlined buttons in a distinct Quick Actions card; status and logs have their own row. No scientific algorithm, source array, analysis-participation or export-range behavior changed.

## Verification

- Native desktop capture now shows the complete caption and all three system buttons with a positive startup Y origin (10 in the capture metadata), not a detached client mock.
- Actual pointer title-drag changed the desktop origin from (68,10) to (18,25). Actual maximum/restore clicks changed the visible window extent and native button state. A coordinate click on Minimize produced a minimized window, then activation restored it. The visible Close button exited the empty application normally.
- Native system Size command and keyboard input changed the observed width from 1427 to 1416. The first attempted pointer edge drag did not resize; do not count that attempt as a passing drag test. Native resizable-frame style bits and the working Size command are verified.
- The in-process tests inspect actual native caption/system-menu/minimize/maximize/thick-frame flags, assert no content replacement/custom WindowChrome, and verify the default HWND bounds fit the current monitor. The test executable uses the same DPI-awareness manifest as the application. Rectangle tests include oversized windows, scaled work-area sizes and negative monitor origins.
- Existing WPF close guards and actual optional literature processing tests pass. Build and scientific tests pass, including the real 78-file batch and the synthetic 100 x 3,101 batch. Existing eighteen pH size/step combinations, eight boundary states, and eighteen main/laboratory client-layout renders cover the documented sizes/scales; native desktop checks are separate.
- The final 1.3.1 installer passes clean per-user installation, installed version/payload hash comparison, repeated installation/update and test-install cleanup. The portable ZIP's application/core/worker are checked against the built payload. No existing laboratory files or browser stores are removed.

## Delivery and limits

The release is 1.3.1; the optional literature CPU pack remains compatible and unchanged. Source and instructions are updated. Physical desktop interaction was checked on this machine's current display setup. Other physical DPI settings, other monitors and other PCs are not claimed as tested; 125/150% alternatives are covered by logical layout/rectangle checks only. Native title-bar colors follow the Windows theme.
