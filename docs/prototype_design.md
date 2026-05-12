# Prototype Design Memo

This local memo summarizes the Notion design notes so Codex can refer to the intended prototype behavior while working in the Unity project.

## Research Purpose

Compare interaction methods for operating 2D virtual displays in supine VR.

The study focuses on how users select and manipulate multiple virtual displays while lying on their back, especially when displays overlap from the input ray's point of view.

## Comparison Conditions

### RaycastBaseline

- Uses the right controller ray.
- Operates only the foremost `Display` hit by the ray.
- Selection behavior should be based on `Physics.Raycast`.
- When multiple displays lie along the ray direction, only the first hit is treated as the operation target.

### GazeStickProposed

- Uses Eye Tracking to collect focus candidates.
- Confirms focus with the grip input.
- Uses the stick to operate the cursor after focus is confirmed.
- Candidate collection should be equivalent to `RaycastAll`, allowing multiple `Display` candidates to be considered.

## HMD Forward Fallback

HMD forward is not part of the experimental input method.

It may be used only as a development fallback for cases where Eye Tracking is unavailable or not yet implemented.

## Display Setup

The prototype uses two displays:

- `Display_A_Front`
  - Front display.
  - Lower placement.
  - Smaller size.
- `Display_B_Back`
  - Back display.
  - Upper placement.
  - Larger size.

Display placement is determined relative to the HMD.

Participants should not be able to freely move displays during the experiment.

## Display Structure

Each display should be generated with the following structure:

- World Space Canvas
- Visible Panel
- Transparent HitPlane
- Cursor

The `Visible Panel` represents the visual surface of the display.

The `Transparent HitPlane` is the physical input target used for ray-based selection and candidate detection.

The `Cursor` represents the display-local cursor controlled by the current interaction method.

## Occlusion Model

Occlusion should be implemented as input-ray occlusion, not as complete visual occlusion.

This means overlapping displays do not need to visually hide each other perfectly. The important behavior is that ray-based input produces different hit ordering depending on the selected layout.

For the baseline condition, `Physics.Raycast` should select only the foremost hit.

For the proposed condition, `RaycastAll`-equivalent behavior should gather multiple display candidates along the ray.

## DisplayLayoutManager

Create a `DisplayLayoutManager` responsible for placing the displays relative to the HMD.

It should support the following presets:

- `NoOcclusion`
- `PartialOcclusion`
- `StrongOcclusion`

The presets should control the relative placement of `Display_A_Front` and `Display_B_Back` so the prototype can switch between different input-ray overlap conditions.

## Phase 1 Scope

Prioritize the following in Phase 1:

- `DisplaySurface`
- `DisplayLayoutManager`
- Generation of two displays:
  - `Display_A_Front`
  - `Display_B_Back`

Do not implement the following yet:

- Click behavior
- Scroll behavior
- Full Eye Tracking input behavior
- Participant-driven free movement of displays during the experiment

