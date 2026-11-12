# Car Parking Challenge

A 3D parking game in Unity 6000.6.0f1. Six lots of increasing difficulty — drive the car into the
highlighted bay without scraping the scenery, before the clock runs out.

## Play it

```bash
open ~/Documents/Hermes/car-parking/Builds/CarParking.app
```

Or open the project in Unity Hub and press Play with `Assets/CarParking/Scenes/Parking.unity` open.

**Controls** — `W/S` or `↑/↓` throttle & brake/reverse · `A/D` or `←/→` steer · `Space` handbrake ·
`C` toggle chase / overhead camera · `R` retry the lot · `N` next lot · `1`–`6` jump to a lot.

## The six lots

| # | Lot | What it tests |
|---|---|---|
| 1 | Empty Lot | straight head-in, no obstacles |
| 2 | Between Neighbours | the two adjacent bays are occupied |
| 3 | Turn In | drive up the aisle, then turn into a row running across your path |
| 4 | Tight Aisle | a 5 m aisle — easier to reverse in than to swing round |
| 5 | Parallel Parking | along the kerb, between two parked cars |
| 6 | Full House | a barrier with one way through, then the far corner bay |

## How a park is judged

A bay is a rectangle on the ground. The car is accepted when **both** hold:

- all four corners of the car's footprint (`CarController.footprintSize`, 1.80 × 4.10 m) are inside
  the bay rectangle, allowing `ParkingBay.EdgeTolerance` = 0.10 m of overshoot, and
- the car's heading is within `ParkingBay.NeatToleranceDeg` = 22° of the bay's long axis — nose-in
  and tail-in both count.

and that has to hold for `ParkingManager.holdSeconds` = 0.7 s, so you cannot fly through the bay and
be credited for it. The HUD's alignment meter is the same number: 60 % corner fit, 40 % squareness.

Touching anything carrying an `Obstacle` component (parked car, cone, pillar, barrier) at an impact
above 0.6 m/s counts as a scrape. Three scrapes, or running out the clock, fails the lot. Best times
are kept per lot in `PlayerPrefs`.

## Rebuild (headless, no Editor UI needed)

```bash
BL=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
"$BL" -batchmode -quit -nographics \
  -projectPath ~/Documents/Hermes/car-parking \
  -executeMethod CarParking.EditorTools.GameBuilder.BuildAll \
  -logFile /tmp/parking_build.log
grep -E "error CS|\[Levels\]|\[GameBuilder\]|Build (Succeeded|Failed)" /tmp/parking_build.log
```

`BuildAll()` sets the player settings, then rebuilds `Assets/CarParking/Scenes/Parking.unity` from
code and builds `Builds/CarParking.app`. **`-projectPath` is not optional** — in batchmode Unity
ignores the working directory and opens the last-opened project instead.

Everything in the scene is generated: there is no hand-authored YAML to keep in sync, so the scene
can always be regenerated from `GameBuilder.BuildScene()`.

`GameBuilder.ReportLevels()` (also a menu item) sanity-checks all six lots on paper — every bay
inside the perimeter, every spawn inside the lot, no parked car on top of the spawn — and logs a
warning per problem, so a bad coordinate surfaces in the build log instead of as an unfinishable lot.

## Layout

```
Assets/CarParking/
  Scripts/
    CarController.cs      arcade Rigidbody controller, retuned for parking; owns the footprint box
    CarFactory.cs         builds and paints cars through Resources, so editor and player agree
    CameraRig.cs          chase camera + overhead mode (C)
    ParkingBay.cs         the bay rectangle, the neatness test and the alignment score
    ParkingManager.cs     lot flow, countdown, scrapes, success/failure, best times
    ParkingHud.cs         IMGUI HUD — no Canvas, no UI packages
    LotBuilder.cs         generates a lot's geometry from a LevelDef (idempotent)
    LevelDefs.cs          the six lots as data
    CollisionReporter.cs  forwards real collisions to the manager, with a cooldown
    Obstacle.cs           marks scenery the player is penalised for touching
    BayPulse.cs / Bobber.cs   the target bay's glow and beam
  Editor/GameBuilder.cs   builds the scene and the player from code
  Resources/
    car.fbx               the car (imported at globalScale 0.01 — it is authored in centimetres)
    Materials/            every material, created by GameBuilder
  Scenes/Parking.unity    generated
```

## Notes from building it

- **The car FBX is authored in centimetres.** `useFileScale` alone leaves it 442 m long and the
  camera ends up inside the mesh; `GameBuilder` pins `ModelImporter.globalScale = 0.01`.
- **The car model faces −Z after import** while the controller drives the parent's `+Z`.
  `CarFactory` detects this by checking whether a front wheel sits behind the origin, and rotates
  the model 180°.
- **The lot is rebuilt at runtime** on every lot change, so `LotBuilder.Build()` clears its own
  children first. During the briefing phase the car's controls are locked and collisions are
  ignored, which also covers the one frame in which a `Destroy` is still pending.
- **Obstacles are marked with a component rather than a layer**, so no `ProjectSettings/TagManager`
  edit is needed and the scene stays fully regenerable.
- **`Run In Background` is on** — without it the game loop freezes whenever the window loses focus,
  which makes Play mode look broken while every value read back still looks correct.
- **`-projectPath` is mandatory in batchmode** (see above). Omit it and Unity opens the last-opened
  project: the log shows no compile and no builder output and the run exits 1, which reads exactly
  like a build that never happened.

## Automated verification

The rules are checked against the **shipped player**, headlessly, by a harness compiled into the
game. It drives the real `ParkingManager` through its real Update loop — load a lot, put the car in
a known pose, let the game decide, assert the verdict. Nothing is mocked.

```bash
~/Documents/Hermes/car-parking/Builds/CarParking.app/Contents/MacOS/CarParking \
  -batchmode -nographics -selftest -logFile /tmp/selftest.log
grep -E "^(PASS|FAIL|=== )" /tmp/selftest.log     # exits 0 when all pass
```

13 checks, all passing:

```
PASS  lot 1..6 accept a neat park   [phase=Parked align=1.00 cornerScore=1.00 heading=0° worstCorner=0.00m]
PASS  rejects a car 3 m outside the bay   [phase=Playing worstCorner=2.25m heading=0°]
PASS  rejects a car 60° off the bay axis  [phase=Playing headingError=60°]
PASS  accepts a tail-in park              [phase=Parked headingError=0°]
PASS  one scrape is counted but does not fail the lot  [hits=1 phase=Playing]
PASS  three scrapes fail the lot                       [hits=3 phase=Failed]
PASS  running out of time fails the lot                [phase=Failed]
=== RESULT: ALL PASS — 13 passed, 0 failed ===
```

`ParkingSelfTest` only does anything when `-selftest` is on the command line, so it is inert in
normal play.

## Status

Verified: builds clean (`build Succeeded errors=0`, 69 MB player, level report 0 warnings) and
**13/13 self-test checks pass against the built player** — the park rule, both near-miss rejections,
tail-in, the scrape counter and the clock have all been exercised end to end.

Not yet verified: the **visuals**. All reasoning above is numeric. The game has never been seen
rendering — a Unity standalone does not expose a macOS accessibility surface, so window capture and
synthetic input are both blocked, and the Editor route needs the MCP bridge port. Confirming the
camera, the glowing target bay and the HUD layout is still outstanding. **Drive it by hand** — open
the built app and play lot 1, which is the quickest way to confirm it looks right.
