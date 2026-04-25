# Blind Cane Contact Perception Prototype

This prototype simulates direct cane contact. It is not sonar. The player sees no normal object colours. Objects only show small local surface information near recent cane contact points. Foot contact is much weaker, so it gives a little underfoot awareness without looking like another cane hit.

## Files

- `Scripts/CanePrimitiveBuilder.cs` builds a cane from Unity primitive Cubes and a Sphere.
- `Scripts/CaneContactRevealer.cs` detects cane tip contact with SphereCast and OverlapSphere.
- `Scripts/FootContactRevealer.cs` reveals a tiny area near the player's feet.
- `Scripts/PerceivableRevealObject.cs` stores recent contact points and sends them to the reveal material.
- `Shaders/ContactRevealLinesURP.shader` is a simple URP transparent line reveal shader.
- `Materials/M_ContactRevealLines.mat` is the ready-to-use reveal material.

## Recommended GameObject Hierarchy

```text
Player
  Main Camera
  Blind_Cane_Root              created by CanePrimitiveBuilder
    Cane_Handle                built-in Cube
    Cane_Shaft                 built-in Cube
    Cane_Tip_Contact_Sphere    built-in Sphere with Sphere Collider
  FeetContactPoint             optional empty GameObject
```

## Layers

Create these layers in Unity:

- `HiddenNormalWorld`
- `PerceptionReveal`
- `Player`

Put normal coloured environment meshes on `HiddenNormalWorld`.

Put the outline or line reveal meshes on `PerceptionReveal`.

Put the player and cane on `Player`.

## Player Camera Culling Mask

Select the player camera and set `Culling Mask` so it does not include `HiddenNormalWorld`.

For a strict blind prototype, include only:

- `PerceptionReveal`
- optional UI layers

This means normal coloured meshes can remain in the scene, but the player camera will not render them.

## Cane Setup

1. Select `Player`.
2. Add `CanePrimitiveBuilder`.
3. Leave `Build On Start` enabled, or use the component context menu `Build Or Rebuild Cane`.
4. Set `Perceivable Layers` to include `PerceptionReveal`.
5. Tune `Local Position` and `Local Euler Angles` until the cane sits slightly in front of the player and points down.

The generated cane uses:

- a long thin Cube for the shaft
- a small Sphere at the tip
- an optional short Cube handle

The cane uses the same outline shader style as perceivable objects. It is not drawn as a solid white model.

The tip Sphere has a Sphere Collider, but detection is not limited to the tip. `CaneContactRevealer` checks the whole cane segment from the handle/root to the tip, using capsule overlap for current contact and several small SphereCasts while the cane moves. This means the shaft can reveal objects when it brushes or strikes them.

## Foot Perception Setup

1. Add an empty child under `Player` named `FeetContactPoint`.
2. Move it to the player's feet.
3. Add `FootContactRevealer` to `Player`.
4. Assign `FeetContactPoint` to the `Foot Point` field.
5. Set `Perceivable Layers` to include `PerceptionReveal`.
6. Keep `Foot Contact Radius` small, such as `0.18` to `0.25`.
7. Keep `Reveal Radius` small, such as `0.18` to `0.25`.
8. Keep `Foot Reveal Strength` low, such as `0.2` to `0.35`.
9. Leave `Foot Ring Strength` at `0` unless you intentionally want a visible circle at the feet.

This gives the player a tiny local sense of the ground or nearby objects underfoot.

## Marking Objects As Perceivable

For each object the player can perceive:

1. Keep the normal coloured mesh on `HiddenNormalWorld`.
2. Create a duplicate mesh in the same position.
3. Put the duplicate on `PerceptionReveal`.
4. Assign `M_ContactRevealLines` to the duplicate mesh renderer.
5. Add `PerceivableRevealObject` to the duplicate or to a parent object above its colliders.
6. Make sure the duplicate, or a child under the same `PerceivableRevealObject`, has a Collider.
7. Choose a `Surface Pattern` on `PerceivableRevealObject`.

Useful surface patterns:

- `PlainSurface` for walls, floors, and blocks.
- `DirectionalStrips` for directional tactile paving.
- `WarningDots` for warning tactile paving.
- `CrossHatch` for rough or gridded surfaces.

The duplicate will be invisible by default because the shader clips all pixels when there are no reveal points.

## Testing In Play Mode

The `Main` scene includes `Blind_Cane_TestScene_RuntimeBuilder`.

1. Open `Assets/Scenes/Main.unity`.
2. Press Play.
3. Use `WASD` to move the player and camera.
4. Move the mouse to sweep the cane left, right, up, and down.
5. Press `Esc` to unlock the mouse cursor.
6. Move the cane tip or shaft into the ground, curb, wall, block, or pillar.
7. Only a small area around the cane contact point should show white outline and surface shape detail.
8. Sweep the cane across a wall to see a brief trail of recent contact points.
9. Stop touching the object. The revealed areas fade out over `0.3` seconds by default.
10. Walk near or over perceivable ground. The foot revealer should show a very small, faint local area near the player's feet, without a strong white ring.

The player camera is configured as a blind-perception camera. It renders the `Player`, `PerceptionReveal`, and `UI` layers, but not `HiddenNormalWorld`, so normal coloured geometry is hidden during Play Mode.

The Game view should start mostly black, but the cane outline should be visible. If the cane tip touches the ground or a block, white line detail appears only around that contact point.

## Prototype Limitation

The shader draws cube edge lines, simple view-angle silhouettes, and configurable local surface shape patterns near contact points. It does not draw a visible circle around the contact area. This is beginner-friendly and works in URP without imported models or complex render features. It is not a full post-process outline yet, so complex imported meshes would need a stronger edge-detection or duplicate-hull outline later.

Good later improvements:

- add a true mesh edge or silhouette pass
- map more surface patterns to walls, floors, hazards, and tactile paving
- separate cane contact and foot contact colours
- pool reveal points across larger multi-renderer objects
