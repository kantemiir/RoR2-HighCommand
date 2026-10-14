# The heroes' AI (rebuilt 2026-10-14)

The user's request: heroes must be obedient (orders done at once and exactly), aggressive (no standing around while
enemies are near) and effective (good targets, focus fire, every skill used at the right moment), and must not throw
their lives away. Everything here runs on the host only (single player, see CLAUDE.md).

## How the game's own AI works (what we build on)

* A survivor AI is a `CharacterMaster` with `BaseAI` and a list of `AISkillDriver`s (`_ref\ai_drivers.txt`, `hc_dump_ai`).
  Every 1/6..1/5 s `BaseAI.EvaluateSkillDrivers` takes the **first** driver whose conditions hold (skill ready, distance
  to the target measured from the aim origin to the target's centre less both radii, line of sight, health ...). The
  driver says which button to press, how (hold, tap, once), where to aim and how to move (chase, flee, strafe, stop).
* `BaseAI` finds its own target (nearest enemy in a 90 degree cone with line of sight) and retaliates on damage. The state
  machine runs `EntityStates.AI.Walker.Combat`, which walks by the driver's movement type.
* Paths: `NodeGraphNavigationSystem` recomputes an agent's path only every **2 s**, and only **one agent per frame** for
  the whole game. A moving target is chased along a stale path; the path's planned jumps take over the move vector
  (`LocalNavigator.ModifyInputsForJumpIfNeccessary`). `LocalNavigator` steers to a point with wall whiskers and a cliff
  check (`allowWalkOffCliff`), but has no notion of "arrived": a point under the feet makes it run on the spot.
* The survivors' drivers were written for Umbras: they always close in, ultimates come first in the list, some drivers
  only walk (Commando's "back off within 15 m" comes before its shooting rule).

## What was wrong (measured with `hc_ai`, scenario `aibase`)

1. A skill forbidden by the profile (a "burst" ultimate, an "escape" roll) was vetoed at the button, but BaseAI kept
   picking that same first driver: the hero pressed nothing at all (Huntress on hold: 89 % of the time idle).
2. Walking-only drivers blocked the skill drivers after them (Commando never shot closer than 15 m).
3. Movement belonged to the drivers: melee chased along stale paths, everybody walked off cliffs after their prey,
   dashes and leaps carried heroes off hold spots (Mercenary 32-49 m away from "hold position") and down cliffs.
4. The game's path jumps pulled heroes out of their hold circle and over edges.
5. Attack-move fought anything within 42 m through walls and never reached its point; formation slots next to a cliff
   snapped to the node at its foot (the hero walked all the way round to another level); hold took its point mid-air.
6. Fleeing: below the retreat line with any enemy within 38 m the hero ran for up to 25 s, then at once again.
7. Equipment was never used (no survivor driver presses it).

## How it works now

```
OrderService --Issue/Errand--> HeroBrain (MonoBehaviour on the master; thinks every AI/ThinkInterval s)
                                 picks Mode / Goal / Enemy / Tether / StandStill / MobilitySkills
                                 parts: TargetPicker, SurvivalInstinct, LootErrands, EnemySeeker
HeroAIState (EntityState run by BaseAI's state machine; every 0.1 s + inputs every frame)
     Go:    the game's path + StuckRecovery (jump, new path, sidesteps, a hop along the path)
     Fight: the game's skill drivers minus SkillGate's vetoes (the next allowed driver is chosen),
            CombatMover's walking, EquipmentUser
     Cast:  aim, wait, press
```

Priorities of the brain, highest first:
1. orders that are never interrupted: move, retreat, hold, use an object, cast;
2. survival (only during attack, attack-move, patrol, follow, seek, own loot trips, or no order);
3. the current order;
4. no order: the stance.

| Order | What the hero does | Ends |
|---|---|---|
| Move | path to its formation slot, runs when far, ignores enemies | arrived: the slot becomes its post |
| Retreat | to the rally point, always running, its get-away skill when enemies are close | arrived: new post |
| Attack-move / patrol | marches; fights what is within `AttackMoveRange` (seen, or fighting the army), at most `AttackMoveLeash` m from where the fight started, then marches on | arrived (patrol: turns) |
| Attack (focus) | that enemy only, any distance; a cloaked one is waited for 3 s; out of reach 3 times: given up | target dead: stays where it is |
| Hold | stays in a circle of `HoldRadius` m; fights seen enemies its skills reach from there (a melee hero's spit or pylon too); no skill that moves it; pushed off: walks back | another order |
| Stop | drops everything, the place becomes its post | at once |
| Follow | within 7.5 m of the leader, fights near the leader | leader dead |
| Seek enemies / chests | as before (EnemySeeker / LootErrands) | never / nothing left to buy |

No order (stance): aggressive = enemies within the profile's acquire radius (seen, or fighting the army within
`AssistRange` m), fought within the leash around the post; defensive = smaller radius and leash; passive = only who
hits this hero, from where it stands. With nothing to fight: a barrel or an affordable chest nearby, else back to the
post. In a charging zone the zone is the leash and each hero has its own spot.

**Target choice** (`TargetPicker`): cost in meters, lower is better: distance (+3/m of height beyond 2.5 m and +25 for a
flier out of reach for melee heroes), minus: fights this hero 10, fights a friend 5, commander's mark 25, Mending elite 8,
boss 6, elite 3, damage per hit as a share of the hero's health (up to 8), wounded (up to 8), each friend already on it 3
(x `FocusFireWeight`, up to 3 friends), current target 8. A target the hero neither hits nor gets closer to for
`UnreachableSeconds` (twice for a boss) is left alone for 8 s - unless it is seen and within the hero's reach (then
it is a cooldown, not a reachability problem).

**Skills** (`SkillGate`): auto-use switches, then the profile rule per slot: auto / never / escape (hurt or fleeing) /
engage (target at least `engageDistance` away) / burst (3+ enemies near or a boss - or after `BurstHoardSeconds` of a
fight). Skills listed in the profile's `mobility` (slots, or skill names such as FlyUp) are not used on hold, at a
target outside the tether, at a target more than 6 m above or below, or when there is no ground under the way (rays
every 3 m to 5 m past the target). A forbidden driver is replaced by the first allowed one (`BaseAI.EvaluateSingleSkillDriver`).

**Walking in a fight** (`CombatMover`): melee: straight at the target when it is seen, within 18 m and on the same level,
else along the path; three heroes on one target come from slightly different sides. Ranged: keep `FightRange` =
min(profile standoff, primary reach - 2) measured as the drivers measure; closer than 60 %: back off; else sidestep.
Drivers keep two moves: standing still to channel (Stop) and running away while a blink fires along the move direction.
Everything is clamped to the tether; the path's jumps only while the hero really follows the path.

**Running away** (`SurvivalInstinct`): only when below `retreatHealth` AND hit in the last 2.5 s or a monster within 15 m is
after this hero. Ends when nobody is after it any more, at `recoverHealth`, or after `FleeMaxSeconds` (then no running
for `FleeCooldownSeconds`). Goal: a healthy friend who is not in the middle of the monsters, else a node on the same level
away from them.

**Equipment** (`EquipmentUser`): config lists `EquipmentInCombat`, `EquipmentWhenHurt` (below half health), `EquipmentVsBoss`;
aims at the target until the aim is on it (at most 1 s), presses once, waits 2 s before another try.

## Tuning

* Global: config section `[AI]` (`src\Core\AiTuning.cs`), most also in Mod Options.
* Per survivor: `profiles\hero_profiles.json` (format 2; an older file is saved as `.old` and replaced). Missing fields
  take the built-in values (`ProfileStore.Defaults`).

## How to check

* `hc_ai reset` ... `hc_ai` per hero: share of time per mode, `passive` (no skill for 1.5 s while a seen enemy is within
  30 m and the order allows fighting), `standing` (the same and not moving), skills per slot, damage, deaths, move orders
  done/dropped, reaction time, speed, drift on hold, time in the zone, equipment uses. `hc_ai trace on` prints every
  hero's state each second.
* `hc_order <move|amove|hold|stop|retreat|patrol|seek|attack|attackbig|follow> [dx dz]`, `hc_spawn_at`, `hc_hurt`,
  `hc_equip`, `hc_warp tp`.
* Scenarios: `aibase` (the comparison, on Titanic Plains), `aiedge` (fleeing, equipment, teleporter, crowd cost, death and
  revival, new stage), `aitrace`, `aimelee`, `aiboss` (second-by-second traces).
