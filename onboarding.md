# MonoTanx onboarding

This document is the quick reference for the current MonoTanx prototype. It describes what the game does now, the map data contract, and the main extension points.

## Project shape

- MonoGame DesktopGL on .NET 9.
- The game starts directly in `GameStage`.
- The logical game surface is 800×600, rendered fullscreen with proportional scaling and centered letterboxing when necessary.
- The world is a hand-authored orthogonal Tiled map. `arena_01.tmx` is 60×40 tiles at 16×16 pixels (960×640 world pixels).
- The top 80 logical pixels (five tiles) are reserved for the HUD. The camera view is the remaining playfield below the HUD.
- World entities use floating-point pixel positions. Tile queries use integer tile coordinates. Entity positions are centers.

## Controls

### Player 1

- `W`: move forward
- `S`: reverse
- `A` / `D`: turn left/right
- `Space`: fire
- `F1`: refill Player 1 fuel and ammunition (does not reset Player 2 or health)

### Player 2

- Normally computer-controlled (`Player2.IsComputerControlled = true`).
- If switched to human control later: cursor keys move/turn and `Enter` fires.

### Global

- `F5` held: pause the simulation and show the debug overlay.
- `Esc`: exit the application.

## Player model

`MonoTanx.Core.Player` owns player-specific defaults and state.

Each player has:

- Name and sprite asset name.
- `IsComputerControlled` flag.
- Position, heading, animation state.
- Health: 100 maximum/starting health.
- Fuel: 200 maximum/starting fuel, displayed as a percentage.
- Movement speed: 120 pixels/second on normal ground.
- Reverse speed multiplier: 0.5 (reverse is always half forward speed).
- Turn speed: 2.5 radians/second.
- Collision radius: 6 pixels.
- Up to two ammunition slots.

The first ammunition slot is consumed completely before the second is used. Ammunition is represented by `AmmunitionSlot` instances, each containing an ammunition type and remaining quantity.

## Movement and collision

- Movement is continuous in world pixels but validated against the tile map.
- Horizontal and vertical movement are resolved separately, allowing sliding along obstacles.
- A player cannot leave the map, enter blocked terrain, or overlap the other player.
- Fuel is consumed while turning and driving.
- Forward fuel rate is 4 units/second; reverse is 8 units/second.
- Terrain can multiply movement speed and fuel cost independently.
- At zero fuel, the player cannot move or turn, but firing remains possible.

## Terrain rules

`TerrainKind` describes interaction semantics; numeric properties tune traversal.

Current movement blocking:

- Ground: traversable
- Bridge: traversable, including over water
- Water: blocked
- Wall: blocked
- Ravine: blocked
- Hill: blocked
- Reflective: blocked for tanks
- Out of bounds: blocked

Current projectile/vision behavior:

- Water and ravines do not block projectiles.
- Walls and hills stop projectiles and block vision.
- Reflective terrain reflects projectiles and blocks vision.
- Out-of-bounds stops projectiles and blocks vision.

Tileset properties are read from the external TSX:

- `TerrainKind`
- `MovementSpeedMultiplier` (float, default 1.0)
- `FuelCostMultiplier` (float, default 1.0)

This favors combinations such as a traversable bridge over water without creating enum variants such as `Ground-Muddy` or `Ground-Icy`.

## Camera and viewpoint

- The camera follows Player 1.
- Player 1 remains near the center of the playfield while the map can scroll.
- At map edges, the camera clamps and Player 1 moves away from center.
- Player 2 may be outside the visible viewport; current gameplay remains keyed to Player 1’s viewpoint.

## Shells and damage

The current default ammunition is a standard shell:

- 20 starting shells per player
- 3-second reload
- 5-second maximum flight time
- Damage: 4
- Shell speed: 260 pixels/second

Shells:

- Continue in the heading held at fire time.
- Are removed on expiry, out-of-bounds, or impact with walls/hills.
- Pass over water, ground, and ravines.
- Can hit either player, including the firing player after a ricochet.
- Reduce health by the ammunition type’s `Damage` value.
- A player reaching 0 health ends the game.

### Reflective surfaces

Reflective tiles are treated as axis-aligned mirrors:

- A horizontal run of reflective tiles flips the shell’s Y velocity.
- A vertical run flips the shell’s X velocity.
- Isolated tiles fall back to a velocity-based axis choice.
- A short reflection cooldown prevents repeated reflections while a shell remains inside one tile.

On impact, the struck player receives a small valid knockback, a slight heading disruption, and a brief screen shake.

## HUD and debug overlay

The HUD is a fixed screen-space layer with a dark metallic-grey background.

Player 1 (left three quarters):

- HP percentage bar
- Shell count
- Reload `!` indicator
- Fuel percentage bar

Player 2 (right quarter):

- HP percentage bar
- Reload `!` indicator
- Fuel percentage bar

Fuel bars show remaining fuel in yellow and depleted fuel in red.

While holding `F5`, the overlay pauses the simulation and shows:

- Both player positions and tile coordinates
- Both headings in radians and degrees
- Player 2 AI mode and route progress
- AI fire cooldown and retaliation timer
- Loaded pickup count

## Player 2 computer behavior

Player 2 currently uses simple state-priority behavior:

1. Retaliation after being hit
   - Stops moving for approximately 1.5 seconds.
   - Turns toward Player 1.
   - Gets a shot opportunity as soon as it is aligned and reloaded.

2. Pickup seeking
   - If fuel is below 50% or ammunition is below half its starting quantity, seeks the nearest relevant active pickup.
   - Pickup seeking takes priority over normal combat and fleeing.

3. No-ammunition evasion
   - If no suitable pickup is available and ammunition is empty, continuously drives away from Player 1.
   - Existing collision resolution handles obstacles.

4. Long-range pursuit
   - When more than half the arena width away, chooses a heading toward Player 1 once and continues on that course without recalculating A* every frame.
   - Returns to close-range behavior once nearer.

5. Close-range combat navigation
   - Uses cached tile-grid A* routing toward a stand-off ring around Player 1.
   - Routes are rebuilt when Player 1 moves or the current route is exhausted.
   - When Player 1 is nearby and visible, Player 2 holds position while aiming rather than letting navigation overwrite its heading.

### Player 2 firing

Player 2:

- Requires line of sight to Player 1; walls, hills, and reflective surfaces block firing.
- Uses an aim tolerance rather than perfect accuracy.
- Applies a reaction delay and fire cooldown.
- Continues correcting its heading toward Player 1 even when terrain currently blocks the shot.

AI tuning properties live on `Player`:

- `PreferredCombatDistanceTiles`
- `LongRangePursuitDistanceFraction`
- `ComputerAimToleranceRadians`
- `ComputerReactionDelaySeconds`
- `ComputerFireCooldownSeconds`

## Tiled pickup contract

Create an object layer named `Pickups` in the map. Place point or rectangle objects on it.

Objects should use custom classes/types:

- `Fuel`
- `Ammunition`

Useful class properties:

- `Amount` (integer)
- `AmmunitionId` (string, ammunition objects)
- `SpriteAsset` (string, e.g. `Sprites/fueldrop_1`)

If `Amount` or `SpriteAsset` is not serialized on an instance, the loader supplies defaults:

- Fuel amount: 50
- Ammunition amount: 5
- Fuel sprite: `Sprites/fueldrop_1`
- Ammunition sprite: `Sprites/ammodrop_1`

Pickup locations are authored in Tiled, but pickups are runtime entities. They animate from four-frame 64×16 sheets (four 16×16 frames), can be collected, and then become inactive/disappear. Fuel restores up to the player’s maximum. Ammunition is added to the matching slot or the first slot.

## Current limitations and likely next steps

- Player 2 does not yet deliberately plan routes to pickups beyond nearest-target routing.
- Pickup respawn, aircraft drops, and dynamic spawning are not implemented.
- Ammunition switching is automatic only; no manual selection mechanic exists yet.
- Projectile reflection is intentionally approximate and should later use exact tile-edge normals or authored surface metadata.
- There is no projectile-vs-projectile, tank armor, score, round reset, or explicit game-over screen yet.
- Destructible terrain, fog of war, advanced AI difficulty, second-human-player mode, and fuel/ammo balancing remain future work.
