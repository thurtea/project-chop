# Chopper character and props: production brief

Status: v1, 2026-10-11. Owner: manager. Branch: `main`.

## Concept sources

All in `Chopper/Art/Concepts/`. Reference only: do not trace them as blueprints, do not put them in any export.

| File | Role |
|---|---|
| `chopper-3D-concept-3.jpg` | Base Worker. Canonical body proportions and clothing. |
| `chopper-3D-concept-1.5.jpg` | Warrior. Canonical equipment sheet: helmet, axe in right hand, shield on left arm. |
| `chopper-3D-concept-1.8.jpg` | Elite (left: without weapons, right: equipped). Steel pauldrons, plated vest. Week 3 or later. |
| `chopper-3D-concept-1.7.jpg` | Double-headed axe turnaround. Canonical axe. |
| `chopper-3D-concept-1.jpg` | Mood and environment only. |
| `chopper-3D-concept-4.jpg` | Voxel pine tree on a dirt base. |

`chopper-3D-concept-2.jpg` (repo root) is a byte-identical copy of 1.5 and is not used.

## Style

- Blocky, chunky, voxel-adjacent low poly. Cube head, square hands, box limbs, about 4.5 heads tall.
- Flat shading. Small bevels only where they help the silhouette read at third-person distance.
- Detail goes in the texture, never in geometry: plaid, stitching, patches, leather grain, chainmail rings, wood grain, knotwork, rivets.
- Palette: red plaid shirt, brown leather vest with gray patches, darker brown belt with steel buckle, gray chainmail skirt with a red shirt hem below it, dark brown trousers, brown boots with leather wraps, ginger hair and beard, green eyes, steel-gray metal.

## Canonical variants

| Variant | Modules |
|---|---|
| Worker | body, head, hair, beard, shirt, vest, belt, chainmail, boots |
| Warrior | Worker + helmet (hair hidden) + axe (Weapon_R) + shield (Shield_L) |
| Elite (later) | Warrior with pauldrons and plated vest in place of the leather vest |

Hair and helmet are mutually exclusive. All modules are skinned to the one shared armature, except axe and shield, which are separate rigid meshes parented to sockets.

## Module list

| Module | Object name | Notes |
|---|---|---|
| Body | `chopper_body` | Skin: hands, forearms below the bracers, neck. Trousers can be part of the body mesh. |
| Head | `chopper_head` | Cube head, face in texture (eyes, brows, nose block). |
| Beard | `chopper_beard` | Big block beard with a braided tip and steel bead. |
| Hair | `chopper_hair` | Swept-back ginger block hair. |
| Shirt | `chopper_shirt` | Red plaid, rolled cuffs, hem showing under chainmail. |
| Vest | `chopper_vest` | Patched leather vest with the diagonal strap and buckle. |
| Belt | `chopper_belt` | Belt, buckle, hanging tongue, leather bracers. |
| Chainmail | `chopper_chainmail` | Short skirt from belt to mid thigh, split front. |
| Boots | `chopper_boots` | Boots plus shin wraps. |
| Axe | `chopper_axe` | Double-headed, per concept 1.7. Separate mesh. |
| Shield | `chopper_shield` | Kite-heater shape, planks, steel rim, boss, axe and two pines painted. Separate mesh. |
| Helmet | `chopper_helmet` | Steel cap, skull front, two horns, nose guard. |
| Tree | `prop_pine_voxel` | Voxel pine on a dirt base (concept 4). Separate prop file. |

## Technical rules

- 1 Blender unit = 1 m. Character 1.80 m tall (sole to top of hair). Feet on Z = 0, facing -Y in Blender (+Z in Unity).
- Transforms applied on every exported object (location of the character at origin, rotation 0, scale 1).
- Triangle budgets: body plus clothing 4,000-8,000; beard, hair, helmet 1,000-3,000 each; axe 300-1,000; shield 500-1,500; full equipped Warrior 8,000-15,000.
- One texture atlas, 2048 or 1024 square, at most 4 materials across the character (target: `M_Chopper` for the character, `M_Chopper_Gear` for axe, shield, helmet).
- One humanoid armature `chopper_rig`, A-pose bind (arms about 45 degrees down), bones named for clean Unity Humanoid auto-mapping, plus sockets `Weapon_R` (right hand), `Shield_L` (left forearm), `Shield_Back` (upper spine).
- Animations: Idle, Walk, Run, Sprint, CrouchIdle, CrouchWalk, Air, LandHard, Roll, SwimIdle, Swim, Attack_1, Attack_2, Attack_3, HitReact, Death. Style (user, 2026-10-12): blocky like Minecraft and survival ARPGs, no ragdoll looseness: rigid torso and head, pendulum limbs, planted feet; whole-body moves turn the body as one block.
- FBX: Apply Scalings FBX All, Forward -Z, Up Y, Add Leaf Bones off, Bake Animation on, only selected objects, no images embedded.
- Unity: imported to `Assets/Game/Characters/Chopper/`, Avatar from this model, materials extracted, `Chopper.prefab` with Animator and CapsuleCollider.

## Method

Models are authored by hand in Blender through Python (explicit vertex and primitive placement), not by AI image-to-3D. Each Blender file has a check script that reports dimensions, triangle counts, applied transforms and names, and renders front, side and three-quarter views for side-by-side comparison with the concepts.

## Live game looks (2026-10-12, user)

- Chopper starts unarmoured: hair, plaid shirt, patchwork vest, crossbelt, belt, trousers, boots, the bear-head axe (`chopper_axe_bear`). No helmet, chainmail, bracers or shield.
- Armoured: after an armour kit (dropped by each felled boss tree) the helmet, chainmail, bracers and the shield on the back show and the hair hides.
- Concept 2.0 is the target for the whole world and the characters; forests differ only in colour.
- 2026-10-12: beard, hair and boots are single forms (detail in the atlas). The chest is broader. The helmet has curved horns and a painted skull. Chainmail reads as rings. Export keeps a crease only where the edge is sharper than 55 degrees.
