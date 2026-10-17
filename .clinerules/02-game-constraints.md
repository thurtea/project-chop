# Gameplay System Constraints
- Motion tuning is hard-locked: move 6, sprint 9.5, crouch 2.6, jump 1.05, gravity 26.
- All acceleration, deceleration, and turning mechanics live strictly in `Core/SteerRules.cs`.
- Player weapon skills are restricted to: Axe (Combat), Sword, Mace, and Spear.
- Ranged weapons are completely excluded. Reject any ranged logic implementations.
- Tree logging functions must stay separate from axe combat structures.
