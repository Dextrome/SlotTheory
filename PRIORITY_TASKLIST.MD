1. Fix duplicate perk application on spam click

Severity: Critical
Effort: Low–Medium
Player impact: High

Why this matters:
This is an actual gameplay exploit, not cosmetic weirdness. If a player can double-apply a modifier by spam clicking, it damages balance, trust, and perceived solidity.

Likely area:
Modifier confirm flow, input guard window, assignment confirmation state, duplicate event suppression.

Done when:
A modifier can only be applied once per pick no matter how fast the player clicks/taps.

2. Fix pause-at-last-hit state corruption

Severity: High
Effort: Medium
Player impact: High

Why this matters:
Green screen + stale previous UI is exactly the kind of bug that makes a game feel amateur even if the rest is polished. Sounds like a race between pause, wave-end transition, and next-state UI construction.

Likely area:
Pause handling, wave-complete transition, draft panel rebuild, overlay cleanup, scene/UI state synchronization.

Done when:
Pausing near wave end cannot produce visual corruption or stale UI remnants.

3. Fix speed reset inconsistency across returning to menu/new run

Severity: Medium
Effort: Low
Player impact: Medium

Why this matters:
Not devastating, but it makes the game feel sloppy and stateful in the wrong way.

Likely area:
Run teardown, speed-state reset, main menu navigation, new run init.

Done when:
Every fresh run starts in the intended speed state, and the speed toggle cycle remains consistent.

4. Add enemy information surface

Severity: High
Effort: Medium
Player impact: High

Why this matters:
This is the strongest design/UI suggestion in the feedback. The player noticed that towers expose rich info, but enemies don’t. That asymmetry creates avoidable opacity.

Best first version:
Enemy hover/selection tooltip with:

enemy type

HP/current HP

speed

leak cost

maybe status flags

Likely area:
Enemy hover hit detection, tooltip system, mobile equivalent selection behavior.

Done when:
Players can inspect enemies with roughly the same clarity they can inspect towers.

5. Add Enemies tab to Slot Codex

Severity: Medium–High
Effort: Low–Medium
Player impact: High

Why this matters:
This is clean, contained, and aligns with the player’s mental model. It also helps surface existing enemy design work you’ve already done.

Good content for first pass:

Basic / Armored / Swift

HP profile

speed

leak cost

behavior/role

status interactions if relevant

Done when:
Players can find enemy information outside live gameplay, not only in How To Play.

6. Investigate right-side empty space on wide screens

Severity: Medium
Effort: Medium
Player impact: Medium

Why this matters:
Even if aspect-ratio-specific, the player noticed it and interpreted it as emptiness / possible missing content. That’s perception debt.

Goal:
Figure out whether this is:

harmless widescreen slack,

poor layout anchoring,

or a real opportunity for useful contextual content.

Possible fixes:

rebalance layout width usage

add low-noise contextual paneling

slightly expand background liveliness

use space for non-cluttering status/info

Done when:
Wide screens stop feeling oddly empty.

7. Review music fatigue / loop harshness

Severity: Medium
Effort: Medium
Player impact: Medium–High

Why this matters:
The player explicitly liked SFX but not the music layer. That’s useful isolation. It suggests the problem is not your whole audio identity.

Likely problem types:

loop fatigue

insufficient variation

repetitive phrasing

maybe certain frequencies getting tiring

Best next move:
Do not overreact. First inspect:

loop length

repetition rate

mix harshness at longer sessions

whether tension ramps help or worsen fatigue

Done when:
Music feels less tiring over repeated runs.

8. Add subtle background motion/liveliness

Severity: Medium
Effort: Low–Medium
Player impact: Medium

Why this matters:
The foreground is apparently lively enough that the background deadness becomes noticeable by contrast.

Important constraint:
Do not clutter gameplay readability.

Good candidates:

subtle grid drift

ambient pulses

faint animated parallax

tiny environmental motion tied to waves/surges

Done when:
Background feels alive without stealing attention.

9. Preserve / reinforce what is clearly working

Severity: Strategic
Effort: Ongoing
Player impact: Very High

From the feedback, these are confirmed strengths:

visual stimulation / movement / bounce

beginner-friendly first run

lenient early waves

experimentation with towers/perks

aesthetic identity

card offering shift when all tower slots are filled

curiosity around wave names / flavor

Why this matters:
Don’t lose sight of what’s already landing while chasing fixes.


-------------------


Best order to tackle
Tier 1 — Fix now

Duplicate perk exploit

Pause-at-last-hit visual corruption

Speed reset inconsistency

Tier 2 — Strongest design value

Enemy hover/info surface

Enemies tab in Slot Codex

Tier 3 — Perception/polish gains

Wide-screen empty-space investigation

Background liveliness pass

Music fatigue review