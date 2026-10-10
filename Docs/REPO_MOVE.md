# Moving Chopper into the private 3D-Game repository

Updated 2026-10-02. Run these in PowerShell yourself; I only prepare files and run `git add .`.

You already pushed the first commit (README) to `thurtea/3D-Game`. With a paid plan, GitHub Pages works from this private repository, so the website can live here too.

## Step 1. Back up Chopper's old history, then remove its nested repository

Close Unity, Visual Studio and VS Code first.

```powershell
cd E:\Projects\3D-Game
Copy-Item -Recurse Chopper\.git _backup\chopper-history.git      # full old history, kept on this PC (ignored by git)
Remove-Item -Recurse -Force Chopper\.git
```

This step is required. While `Chopper\.git` exists, Git treats Chopper as a nested repository and uploads none of its files.

## Step 2. Add everything and push

```powershell
cd E:\Projects\3D-Game
git add .
git status                      # check: no Library, Temp, Logs, Builds or .vs folders in the list
git commit -m "Add Chopper, the tutorial project and the workflows"
git push
```

If `git status` lists something large you do not want, add it to `.gitignore`, run `git rm -r --cached <path>`, then `git add .` again.

## Step 3. Website

1. In thurtea/3D-Game, open Settings, Pages, and set Source to **GitHub Actions**. The workflow `.github/workflows/chopper-pages.yml` publishes `Chopper/Website` whenever it changes.
2. A project site is served under its repository name, so the address becomes **thurtea.com/3D-Game/** instead of thurtea.com/Chopper/.
   - To keep the old address instead, keep a repository named `Chopper` for the website only (the old one, emptied, or a new one), and leave Pages off in 3D-Game.

## Step 4. The old Chopper repository

Once step 2 is pushed and the site works where you want it, delete thurtea/Chopper: Settings, Danger Zone, Delete this repository. The old history stays in `_backup\chopper-history.git` on this PC.

## Releases for players

People without access to a private repository cannot download its releases, even on a paid plan.

For public playtests, attach the zips somewhere public: a public repository's Releases page, itch.io, or a shared drive link. For private testers, invite them as collaborators on 3D-Game.

To make the zips:

1. In Unity, choose Chopper > Build > All Platforms (Release).
2. In the Chopper folder, run `python Tools/package_release.py Builds/Release <version>`.

The optional cloud build workflow is `.github/workflows/chopper-release.yml`, run on a tag like `chopper-v0.3.0`. It needs the Unity secrets described at the top of that file.
