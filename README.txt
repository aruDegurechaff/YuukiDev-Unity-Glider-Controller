# YuukiDev Glider Controller

By Aldreck Paul L. Obenario (YuukiDev)

A modular glider controller package for infinite flight games. Includes movement, power-ups, scoring, and proximity-based feedback systems.

---

## Features
- Smooth gliding movement with boost and slow states
- Low-speed fall behavior and dynamic drag tuning
- Power-ups: Wind Orb, Phantom Grace, Lantern Sparks, Feather Slip
- Coin collectibles with dynamic spawners
- Proximity graze system for scoring and camera feedback
- Stamina UI and power-up buff UI helpers
- Game over and revive flow

---

## Requirements
- Unity 6000.2 or newer
- Input System 1.7.0 (package dependency)
- URP is required only if you use the camera proximity VFX

---

## Installation

### Local (embedded)
1. Place the package folder under `Packages/`
2. Or add a `file:` reference in `Packages/manifest.json`

Example `manifest.json` entry:
```json
"com.yuukidev.glidercontroller": "file:../path/to/YuukiDev Glider"
```

---

## Quick Start
1. Create a player object with `PlayerController`, `Rigidbody`, `Collider`, and `YuukiPlayerInput`. Or just use the 'GuyController' prefab in the prefabs folder.
2. Add `CameraFollowAndRotate` to your camera rig and assign the player target.
3. Add `MovementTracker` and `ProximityChecker` to the player object.
4. Add `ScoreManager` to a scene object and assign UI references.
5. Add `CoinsSpawnerManager` and `PowerUpsSpawnerManager` and set the player target (or enable auto-resolve).
6. Optional UI helpers: `SpeedBoostStaminaUI` and `PowerUpBuffFadeUI`.

---

## Notes
- Power-ups and coins auto-despawn when far from the player.
- Phantom Grace ignores obstacle collisions while preserving proximity triggers.
- Proximity VFX disables while Phantom Grace is active.
