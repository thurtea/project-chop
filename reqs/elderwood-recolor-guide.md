# Recoloring the Elderwood Guardian

How to make the element versions of **Monsterlings: Elderwood Guardian** (Unity Asset Store) so each forest has its own tree monster. You do the painting; I do the Unity side.

## 0. Before you paint: check if you need to

Import the pack first (step 1) and tell me. If the leaves or moss are a **separate material** from the bark, I can tint them in code and you can skip this guide entirely. Only follow the steps below if the whole monster uses one texture.

## 1. Import

1. Buy and download it in the Package Manager (My Assets), then Import everything.
2. Move the pack's folder to `Assets/ThirdParty/Monsterlings_ElderwoodGuardian/`. Keep the license or EULA note in that folder.
3. Tell me the folder name. I will check it in Unity 6 with URP (the store page lists 2022.3), set the rig, and swap it in for the walking trees.

Note: the pack has **idle, walk and death** only. There is no attack or hit animation. I will make the attack a lunge and swing in code at first, and if the rig is Humanoid I can borrow attack moves from our animation library.

## 2. What to paint

Paint **copies**, never the pack's own files, so an update cannot overwrite your work.

- Find the base color texture in the pack's Textures folder. It is the colored one, usually named with `BaseColor`, `Albedo` or `Diffuse`. Leave the normal, roughness, metallic and occlusion maps alone.
- Make five copies in a new folder `Assets/_Chopper/Art/Elderwood/`, named exactly:

| File | Forest | Leaves and moss should look like |
|---|---|---|
| `T_Elderwood_Hub_BaseColor.png` | The hub (no element) | Leave as it is: natural green |
| `T_Elderwood_Water_BaseColor.png` | North, Water | Cool blue, like the blue pines (around `#4C8FD6`) |
| `T_Elderwood_Fire_BaseColor.png` | South, Fire | Red to orange, like the red and orange pines (around `#D9542B`) |
| `T_Elderwood_Earth_BaseColor.png` | East, Earth | Dark olive with yellow, like the dark and yellow pines (around `#6B6B2E` and `#C9A73A`) |
| `T_Elderwood_Wind_BaseColor.png` | West, Wind | Pale mint, like the light green pines (around `#A8E0B0`) |

The hex codes are a starting point. Match them by eye to the pine trees in each forest in the game (F5, `tp` to a forest, `hud` to hide the HUD).

**Change only the leaves, moss and any glowing bits. Keep the bark brown in every version**, so it still reads as wood you can chop.

## 3. How to recolor (GIMP, free; Photoshop and Krita work the same way)

1. Open the copy, for example `T_Elderwood_Water_BaseColor.png`.
2. Select the leaves and moss:
   - **Select > By Color**, click a leaf, and raise the Threshold until all leaf areas are picked but the bark is not. Shift-click to add more shades.
   - Or use the **Free Select** (lasso) tool around the leaf areas of the texture. The texture is a flat unwrap, so leaves are usually grouped in islands.
3. **Select > Grow** by 1 or 2 pixels, so no green edge is left where islands meet.
4. **Colors > Hue-Saturation** (or **Colors > Colorize** for a stronger change). Move Hue to the target color, then tune Saturation and Lightness until it matches the forest's trees.
5. **Select > None**, then **File > Export As** PNG with the same name, overwriting the copy. Keep the same size (2048 px). Do not resize or crop, or it will no longer line up with the model.
6. Repeat for each element.

Tips:
- If a seam shows on the model later, the selection missed some pixels at an island edge. Grow the selection a little more and export again.
- Keep the colors a bit muted. Fully saturated colors look plastic next to the pixel-pack trees.

## 4. Hand it back

Put the five PNGs in `Assets/_Chopper/Art/Elderwood/` and tell me. I will make one material per element, pick the right one for each enemy's element, keep the sapling stage (the monster rooted and asleep by day), and check every color in screenshots.

## License

It is a Unity Asset Store package (Asset Store EULA, single entity): it may ship inside the game, but the raw models and animations must never be shared on their own. Keep the receipt or EULA note in the pack's folder, `Assets/ThirdParty/Monsterlings_ElderwoodGuardian/`. The store page is https://assetstore.unity.com/packages/3d/characters/monsterlings-elderwood-guardian-animated-tree-monster-388920 (an older note here linked a different Monsterlings pack by mistake).
