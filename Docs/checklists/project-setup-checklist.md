# Project Setup Checklist

Use this as a baseline for new branches of work, subsystem migrations, or future
project spinoffs. For the current repository, treat each item as a reminder to
check whether the decision is already settled or still open.

- [ ] Confirm the target Unity version and any upgrade constraints
- [ ] Confirm the render pipeline strategy and scene lighting assumptions
- [ ] Confirm the Input System setup and control-scheme expectations
- [ ] Decide whether new runtime-loaded content should remain on `Resources` or
  move behind an ADR-backed loading strategy
- [ ] Confirm namespace and assembly definition strategy before broad refactors
- [ ] Confirm the bootstrap scene and scene routing ownership
- [ ] Confirm how ScriptableObject data should be authored and stored
- [ ] Confirm runtime test and validator expectations for the affected systems
- [ ] Write ADRs for any pipeline-level or migration decisions
- [ ] Update the project baseline docs if project-wide constraints changed
