# MainEscape Game Design Document

## Summary

- Working title: `MainEscape` / `HorrorStealth`
- Genre: 2D top-down stealth horror prototype
- Platform: PC prototype
- Target session length: one short lobby-to-1F run
- Core fantasy: survive a descent through hostile darkness by reading light,
  sound, and enemy pressure better than the space wants you to

## Player Promise

The player should feel tense but informed. Darkness should be threatening
without becoming arbitrary, and every success should come from reading the
space, managing risk, and recognizing danger before it fully collapses into
failure.

## Pillars

1. Limited information creates tension.
2. Darkness should feel hostile but still readable.
3. Local light is safety, navigation, and risk at the same time.
4. The player and enemies should follow legible visibility rules instead of
   arbitrary cheats.

## Current Gameplay Loop

1. Start in the lobby.
2. Begin a run into `5F`.
3. Read the space with flashlight and local light landmarks.
4. Avoid or out-position enemies.
5. Collect required progression items.
6. Unlock the next route.
7. Descend through `5F -> 4F -> 3F -> 2F -> 1F`.
8. Read floor-clear, final-clear, or failure messaging.
9. Return to the lobby summary.

The intended feeling is not pure action. It is cautious movement through
incomplete information.

## Player Readability

- The flashlight is the main information tool.
- Fog-of-war should hide enough to keep tension, but not so much that
  navigation feels blind.
- Small local lights, exit signs, and room lights should create orientation
  anchors.
- Important interactables should either sit in readable light or be supported
  by nearby landmark lighting.
- HUD status should be legible at a glance.
- Quick slots should privilege item icon and quantity over label clutter.
- Health, battery, and threat panels should communicate state before the player
  reads fine text.

## Enemy Readability

- Enemy danger should be understandable before contact.
- Enemy vision should feel threatening, but not like a flat debug triangle.
- If an enemy is hidden in darkness, the player should not get perfect free
  information.
- If an enemy stands in a readable local light pool, both the body and the
  danger zone can be revealed more clearly.
- Investigate and chase states should be distinguishable enough that the player
  understands when pressure has escalated.
- Walking noise should create risk, but it should sit slightly below the
  player's primary recognition envelope so normal movement does not feel
  instantly unfair.

## Current Enemy Roles

### Patrol Guard

- moves through authored patrol routes
- creates repeatable pressure and corridor denial

### Sentry Guard

- anchors a sightline
- turns certain rooms and corridors into watch spaces that the player must
  route around

### Vent Enemy

- adds delayed pressure and uncertainty
- should feel heard before fully understood

## Space Design Rules

- Large dark rooms need at least one landmark light or landmark object.
- Long corridors should be broken by doors, props, light accents, or signage.
- Important route choices should be visible before commitment.
- Cover and movement blockers should shape navigation without turning every room
  into hard maze clutter.

## Progression And Failure

- Progression is still centered on authored markers and authored routes.
- The player should understand why a path is blocked and what unlocks it.
- Goal visuals should reinforce progression objects instead of replacing
  interaction logic.
- Floor-clear messaging should confirm that a route is secured before the next
  floor begins.
- Failure messaging should offer immediate recovery through retry or
  return-to-lobby options.

## Tone Targets

- dread from partial knowledge
- relief from reaching a readable lit zone
- tension from hearing or seeing enemy presence before direct contact
- fair failure caused by readable mistakes, not unreadable darkness

## Content Scope

- Current scope:
  - keep the authored lobby-plus-floor chain playable and readable
  - tune light, fog, enemy visibility, UI, and audio
  - reinforce route clarity and objective understanding
- Explicitly out of scope for now:
  - broad new system expansion
  - detached showcase or test-bay workflows as live content
  - feature work that destabilizes the current authored loop
- Current risks to scope:
  - readability polish turning into structural rework
  - lower-floor drift after 5F-first edits
  - adding new content before the current loop reads cleanly

## Current Focus

The present version is in a readability-and-polish phase.

- Improve visual guidance before adding large new systems.
- Keep the authored working scene and lobby loop stable.
- Tune light, fog, enemy visibility, UI, and audio until the space reads
  clearly without losing mood.

## Open Questions

- How much additional enemy variety does the current loop need before it harms
  readability?
- Which readability improvements should land as durable authored scene rules
  rather than one-off scene polish?
- When should the temporary UI/readability work be treated as final enough to
  lock against broader content growth?
