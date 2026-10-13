"""Author Chopper's takes on chopper_rig: Idle, Walk, Run, Sprint, CrouchIdle, CrouchWalk, Air, LandHard, Roll,
SwimIdle, Swim, Attack_1, Attack_2, Attack_3, HitReact, Death, and the fishing takes FishWait, FishCast, FishFight
and FishCatch, and Gather.

Movement style (user, 2026-10-12): no ragdoll looseness. Blocky and snappy like Minecraft and survival ARPGs: the
torso and head stay rigid (no hip twist, no spine sway, no head bob), the arms swing as near-straight pendulums, the
feet stay planted on the ground (ARPG grounding), and whole-body moves (roll, death, swim) turn the body as one block.

Run:  blender -b -P animate_chopper.py
In:   Art/Blender/chopper_rigged.blend (from rig_chopper.py)
Out:  same file, one NLA strip per action (one FBX take per strip).

Idle, Walk, Run, Sprint, CrouchIdle, CrouchWalk, Air, SwimIdle and Swim loop (first frame copied onto the
last). LandHard, Roll and the combat clips are one-shots: the last frame is the end pose, not a copy of the first.

Method: every frame is computed in closed form. Upper body = FK rotations given as
rest-space axis/angle deltas. Legs = analytic two-bone IK to a foot transform. The
planted foot is attached to a ground frame that moves back at WALK_SPEED (in-place
walk, no root motion) and rolls about the heel (strike) or the toe tip (push-off),
so its contact point never slides relative to the ground. The lowest boot vertex of
a planted foot is snapped to Z=0 every frame. All curves are periodic in the loop
length, so the first and last frames are identical by construction.
"""
import math
import os

import bpy
from mathutils import Matrix, Quaternion, Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
BLEND = os.path.join(ART, "Blender", "chopper_rigged.blend")

FPS = 30
IDLE_LEN = 60          # frames 1..61 (61 == 1)
WALK_LEN = 30          # frames 1..31 (31 == 1)
WALK_SPEED = 0.75      # m/s; Unity should move the character at this speed
RUN_LEN = 18           # frames 1..19
RUN_SPEED = 4.10       # m/s (the live jog, 5.5 m/s, plays it at 1.34x: about 0.45 s a stride)
RUN_STANCE = 0.32
SPRINT_LEN = 16
SPRINT_SPEED = 6.50    # m/s (live sprint 8.7 m/s)
SPRINT_STANCE = 0.24
CROUCH_LEN = 24
CROUCH_SPEED = 1.60    # m/s (live crouch walk 2.6 m/s)
CROUCH_DROP = 0.20     # m the hips sit lower than in the walk
CROUCH_IDLE_LEN = 40
AIR_LEN = 20
LAND_LEN = 18
ROLL_LEN = 24          # 0.80 s; PlayerAnimator.RollClipSeconds must match
ROLL_SKIN = 0.20       # m from the joints out to the surface of the head, back and boots
SWIM_IDLE_LEN = 40
SWIM_LEN = 24
ATTACK1_LEN = 22       # diagonal chop from over the right shoulder, one-shot
ATTACK2_LEN = 22       # diagonal chop from over the left shoulder, one-shot
ATTACK3_LEN = 28       # overhead finisher, one-shot
HIT_LEN = 15
DEATH_LEN = 30
STANCE = 0.60          # fraction of the cycle a foot is on the ground
STRIKE_PITCH = -14.0   # deg, toes up at heel strike (rotation about +X)
TOEOFF_PITCH = 26.0    # deg, heel up at toe off
HEEL_ROLL_END = 0.10
HEEL_OFF_START = 0.40
SWING_LIFT = 0.055
REACH_TARGET = 0.985   # walk pelvis height: legs at this fraction of full length ...
REACH_MAX = 0.992      # ... and never above this (keeps the knee off the IK singularity)
PELVIS_HARMONICS = 4   # smoothing of the derived pelvis curve
FOOT_SHIFT = 0.0       # stance sweep offset along Y (negative = feet further forward)

I3 = Matrix.Identity(3)
X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
Z = Vector((0, 0, 1))


def T(v):
    return Matrix.Translation(Vector(v))


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, Vector(axis).normalized())


def mirror_axis(a):
    return Vector((a[0], -a[1], -a[2]))


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


class Rig:
    def __init__(self, ob, boots):
        self.ob = ob
        self.rest = {b.name: b.matrix_local.copy() for b in ob.data.bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in ob.data.bones}
        self.order = [b.name for b in ob.data.bones]
        self.length = {b.name: b.length for b in ob.data.bones}
        # rigid foot geometry (boot vertices below the ankle blend), per side
        self.foot_pts = {"Left": [], "Right": []}
        for v in boots.data.vertices:
            if v.co.z < 0.13:
                self.foot_pts["Left" if v.co.x > 0 else "Right"].append(v.co.copy())
        self.max_reach = 0.0
        self.max_loc_err = 0.0

    def rest_dir(self, name):
        return (self.rest[name].to_3x3() @ Y).normalized()

    # -------------------------------------------------------------- feet
    def foot_min_z(self, side, F):
        return min((F @ p).z for p in self.foot_pts[side])

    def leg_ik(self, side, M_hips, F):
        ul, ll, ft = side + "UpperLeg", side + "LowerLeg", side + "Foot"
        H = (M_hips @ self.rest["Hips"].inverted() @ self.rest[ul]).translation
        A = F @ self.rest[ft].translation
        L1 = (self.rest[ll].translation - self.rest[ul].translation).length
        L2 = (self.rest[ft].translation - self.rest[ll].translation).length
        dv = A - H
        dist = dv.length
        self.max_reach = max(self.max_reach, dist / (L1 + L2))
        dist_c = min(dist, (L1 + L2) * 0.99995)
        u = dv.normalized()
        pole = F.to_3x3() @ Vector((0, -1, 0))
        w = (pole - u * pole.dot(u)).normalized()
        ca = (L1 * L1 + dist_c * dist_c - L2 * L2) / (2 * L1 * dist_c)
        sa = math.sqrt(max(0.0, 1 - ca * ca))
        K = H + (u * ca + w * sa) * L1
        n = u.cross(w).normalized()

        def frame(head, tail):
            yy = (tail - head).normalized()
            xx = (n - yy * n.dot(yy)).normalized()
            zz = xx.cross(yy)
            m = Matrix((xx, yy, zz)).transposed().to_4x4()
            m.translation = head
            return m

        A_reached = K + (A - K).normalized() * L2
        return {ul: frame(H, K), ll: frame(K, A_reached), ft: F @ self.rest[ft]}

    # -------------------------------------------------------------- solve
    def solve(self, hips_off, deltas, feet):
        """deltas: {bone: 3x3 rest-space rotation}; feet: {side: 4x4 foot transform}."""
        M, B = {}, {}
        override = {}
        for name in self.order:
            par = self.parent[name]
            Rn = self.rest[name]
            if par is None:
                head = Rn.translation
                D = deltas.get(name, I3).to_4x4()
                Mp = T(head + hips_off) @ D @ T(-head) @ Rn
                base = Rn
            else:
                base = M[par] @ self.rest[par].inverted() @ Rn
                if name.endswith("UpperLeg"):
                    side = name[:-8]
                    override.update(self.leg_ik(side, M["Hips"], feet[side]))
                if name in override:
                    Mp = override[name]
                else:
                    D = deltas.get(name)
                    if D is not None:
                        R3 = Rn.to_3x3()
                        Mp = base @ (R3.transposed() @ D @ R3).to_4x4()
                    else:
                        Mp = base
            M[name] = Mp
            B[name] = base.inverted() @ Mp
            if par is not None:
                self.max_loc_err = max(self.max_loc_err, B[name].translation.length)
        return M, B


# ------------------------------------------------------------------ pose building
def sym(deltas, bone_left, axis, deg, rig=None):
    """Apply a rest-space rotation to a left bone and its mirror to the right bone."""
    for side, ax in (("Left", Vector(axis)), ("Right", mirror_axis(axis))):
        name = side + bone_left
        deltas[name] = R(ax, deg) @ deltas.get(name, I3)


def toward(rig, bone, target, deg):
    """Rotation axis that turns the bone's rest direction toward target (mirror-safe)."""
    d = rig.rest_dir(bone)
    t = Vector(target)
    t = (t - d * t.dot(d)).normalized()
    return d.cross(t).normalized()


def arm_base(rig, deltas, adduct, elbow_l, elbow_r):
    # lower the arms from the A-pose toward the body
    for side in ("Left", "Right"):
        ua = side + "UpperArm"
        deltas[ua] = R(toward(rig, ua, (0, 0, -1), 1), adduct) @ deltas.get(ua, I3)
        la = side + "LowerArm"
        ang = elbow_l if side == "Left" else elbow_r
        deltas[la] = R(toward(rig, la, (0, -1, 0), 1), ang) @ deltas.get(la, I3)


def planted():
    return {"Left": Matrix.Identity(4), "Right": Matrix.Identity(4)}


def block_upper(rig, deltas, lean, adduct, swing, phase, elbow_l, elbow_r):
    """Rigid torso, the head kept level, the arms as near-straight pendulums opposite the legs (left leg forward at
    phase 0 -> left arm back)."""
    deltas["Spine"] = R(X, lean * 0.6)
    deltas["Chest"] = R(X, lean * 0.4)
    deltas["Neck"] = R(X, -lean * 0.5)
    deltas["Head"] = R(X, -lean * 0.5)
    arm_base(rig, deltas, adduct, elbow_l, elbow_r)
    c1 = math.cos(2 * math.pi * phase)
    deltas["LeftUpperArm"] = R(X, swing * c1) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, -swing * c1) @ deltas["RightUpperArm"]


def idle_pose(rig, f):
    """Standing still: a slow breath in the chest and arms; the head, hips and legs hold."""
    p = (f - 1) / IDLE_LEN
    br = math.sin(2 * math.pi * p)                 # breath
    deltas = {}
    block_upper(rig, deltas, 1.0, 21.0, 0.0, 0.0, 12.0, 30.0)
    deltas["Chest"] = R(X, -0.8 * br) @ deltas["Chest"]
    for side in ("Left", "Right"):
        ua = side + "UpperArm"
        deltas[ua] = R(toward(rig, ua, (0, 0, -1), 1), -1.2 * br) @ deltas[ua]
    return Vector((0.0, 0.0, -0.004 - 0.003 * br)), deltas, planted()


def stance_F(rig, side, q, y0, speed=WALK_SPEED, length=WALK_LEN, stance=STANCE):
    """Planted foot at local phase q in [0, stance]; pivot heel then flat then toe."""
    g = T((0, y0 + speed * q * length / FPS, 0))
    q = q * STANCE / stance          # the heel and toe roll keep their share of the stance
    x = 0.13 if side == "Left" else -0.13
    heel = Vector((x, 0.140, 0.0))
    toe = Vector((x, -0.250, 0.0))
    if q < HEEL_ROLL_END:
        th = STRIKE_PITCH * (1 - smooth(q / HEEL_ROLL_END))
        F = g @ T(heel) @ R(X, th).to_4x4() @ T(-heel)
    elif q < HEEL_OFF_START:
        F = g
    else:
        s = (q - HEEL_OFF_START) / (STANCE - HEEL_OFF_START)
        th = TOEOFF_PITCH * s * s
        F = g @ T(toe) @ R(X, th).to_4x4() @ T(-toe)
    return T((0, 0, -rig.foot_min_z(side, F))) @ F


def hermite(p0, p1, m0, m1, s):
    s2, s3 = s * s, s * s * s
    return ((2 * s3 - 3 * s2 + 1) * p0 + (s3 - 2 * s2 + s) * m0 +
            (-2 * s3 + 3 * s2) * p1 + (s3 - s2) * m1)


def foot_F(rig, side, q, y0, speed=WALK_SPEED, length=WALK_LEN, lift=SWING_LIFT, stance=STANCE):
    """Foot transform at local phase q. Stance: ground-attached roll. Swing: the foot pitch
    goes from push-off to flat by mid-swing and to the heel-strike pitch at the end; its
    height is set so the lowest boot vertex follows a low clearance arc."""
    q = q % 1.0
    if q <= stance:
        return stance_F(rig, side, q, y0, speed, length, stance), True
    s = (q - stance) / (1 - stance)
    F0 = stance_F(rig, side, stance, y0, speed, length, stance)
    F1 = stance_F(rig, side, 0.0, y0, speed, length, stance)
    ank = rig.rest[side + "Foot"].translation
    A0, A1 = F0 @ ank, F1 @ ank
    dur = (1 - stance) * length / FPS
    m = speed * dur                          # matches ground speed at both ends
    yy = hermite(A0.y, A1.y, m, m, s)
    if s < 0.45:
        th = TOEOFF_PITCH * (1 - smooth(s / 0.45))
    elif s < 0.7:
        th = 0.0
    else:
        th = STRIKE_PITCH * smooth((s - 0.7) / 0.3)
    F = T((A0.x, yy, 0.0)) @ R(X, th).to_4x4() @ T(-ank)
    clear = lift * math.sin(math.pi * s ** 0.65)             # peaks early: toe clearance after push-off
    F = T((0, 0, clear - rig.foot_min_z(side, F))) @ F
    return F, False


_PELVIS_CACHE = {}


def pelvis_heights(rig, pose_fn=None, length=None):
    """Per-frame pelvis Z offset, derived from the feet: as high as possible
    with every leg at REACH_TARGET of its length, then low-passed (periodic, first
    PELVIS_HARMONICS harmonics) and lowered so no frame exceeds REACH_MAX."""
    pose_fn = walk_pose if pose_fn is None else pose_fn
    length = WALK_LEN if length is None else length
    key = (pose_fn.__name__, length, FOOT_SHIFT, REACH_TARGET, REACH_MAX, PELVIS_HARMONICS)
    if key in _PELVIS_CACHE:
        return _PELVIS_CACHE[key]
    saved_reach, saved_gap = rig.max_reach, rig.max_loc_err
    L = {}
    for sd in ("Left", "Right"):
        L[sd] = ((rig.rest[sd + "LowerLeg"].translation - rig.rest[sd + "UpperLeg"].translation).length +
                 (rig.rest[sd + "Foot"].translation - rig.rest[sd + "LowerLeg"].translation).length)
    raw = []
    for f in range(1, length + 1):
        hips_off, deltas, feet = pose_fn(rig, f, pelvis_z=0.0)
        M, _ = rig.solve(hips_off, deltas, feet)
        need = 1e9
        for sd in ("Left", "Right"):
            H = M[sd + "UpperLeg"].translation
            A = feet[sd] @ rig.rest[sd + "Foot"].translation
            dxy = Vector((A.x - H.x, A.y - H.y)).length
            r = REACH_TARGET * L[sd]
            need = min(need, A.z + math.sqrt(max(0.0, r * r - dxy * dxy)) - H.z)
        raw.append(need)
    n = len(raw)
    sm = []
    for i in range(n):
        v = sum(raw) / n
        for k in range(1, PELVIS_HARMONICS + 1):
            a = sum(raw[j] * math.cos(2 * math.pi * k * j / n) for j in range(n)) * 2 / n
            b = sum(raw[j] * math.sin(2 * math.pi * k * j / n) for j in range(n)) * 2 / n
            v += a * math.cos(2 * math.pi * k * i / n) + b * math.sin(2 * math.pi * k * i / n)
        sm.append(v)
    # lower the whole curve until no frame exceeds REACH_MAX
    for _ in range(50):
        worst = 0.0
        for i in range(n):
            hips_off, deltas, feet = pose_fn(rig, i + 1, pelvis_z=sm[i])
            M, _ = rig.solve(hips_off, deltas, feet)
            for sd in ("Left", "Right"):
                d = (feet[sd] @ rig.rest[sd + "Foot"].translation - M[sd + "UpperLeg"].translation).length
                worst = max(worst, d / L[sd])
        if worst <= REACH_MAX:
            break
        sm = [v - 0.002 for v in sm]
    _PELVIS_CACHE[key] = sm
    rig.max_reach, rig.max_loc_err = saved_reach, saved_gap
    return sm


def walk_pose(rig, f, pelvis_z=None):
    p = (f - 1) / WALK_LEN
    if pelvis_z is None:
        pelvis_z = pelvis_heights(rig)[(f - 1) % WALK_LEN]
    hips_off = Vector((0.0, 0.0, pelvis_z))
    deltas = {"Hips": R(X, 2.0)}
    # shield arm: elbow held steady (the shield extends past the elbow; extra flexion would sweep its curved top rim
    # across the upper-arm sleeve)
    block_upper(rig, deltas, 1.0, 17.0, 24.0, p, 12.0, 16.0)
    # feet: left heel strike at p=0, right at p=0.5
    y0 = -WALK_SPEED * STANCE * WALK_LEN / FPS * 0.5 + FOOT_SHIFT
    FL, pl = foot_F(rig, "Left", p, y0)
    FR, pr = foot_F(rig, "Right", p + 0.5, y0)
    return hips_off, deltas, {"Left": FL, "Right": FR}


# Faster gaits: (length, speed m/s, stance, swing lift m, lean deg, arm swing deg, elbow L, elbow R, adduct, hips drop m)
GAITS = {
    "Run": (RUN_LEN, RUN_SPEED, RUN_STANCE, 0.14, 8.0, 40.0, 30.0, 40.0, 17.0, 0.0),
    "Sprint": (SPRINT_LEN, SPRINT_SPEED, SPRINT_STANCE, 0.20, 14.0, 55.0, 60.0, 70.0, 17.0, 0.0),
    "CrouchWalk": (CROUCH_LEN, CROUCH_SPEED, 0.60, 0.07, 22.0, 14.0, 40.0, 45.0, 14.0, CROUCH_DROP),
}


def gait(name):
    length, speed, stance, lift, lean, swing, el_l, el_r, adduct, drop = GAITS[name]

    def pose(rig, f, pelvis_z=None):
        p = (f - 1) / length
        if pelvis_z is None:
            pelvis_z = pelvis_heights(rig, pose, length)[(f - 1) % length] - drop
        hips_off = Vector((0.0, 0.0, pelvis_z))
        deltas = {"Hips": R(X, 2.0)}
        block_upper(rig, deltas, lean, adduct, swing, p, el_l, el_r)
        y0 = -speed * stance * length / FPS * 0.5
        FL, _pl = foot_F(rig, "Left", p, y0, speed, length, lift, stance)
        FR, _pr = foot_F(rig, "Right", p + 0.5, y0, speed, length, lift, stance)
        return hips_off, deltas, {"Left": FL, "Right": FR}
    pose.__name__ = name.lower() + "_pose"
    return pose


run_pose = gait("Run")
sprint_pose = gait("Sprint")
crouch_walk_pose = gait("CrouchWalk")


def crouch_idle_pose(rig, f):
    p = (f - 1) / CROUCH_IDLE_LEN
    br = math.sin(2 * math.pi * p)
    deltas = {"Hips": R(X, 2.0)}
    block_upper(rig, deltas, 22.0, 14.0, 0.0, 0.0, 40.0, 45.0)
    deltas["Chest"] = R(X, -0.8 * br) @ deltas["Chest"]
    return Vector((0.0, 0.0, -CROUCH_DROP - 0.004 * br)), deltas, planted()


def body_turn(hips_off, deltas, feet, rig, axis, deg, pivot):
    """Turn the whole posed body as one block by deg about axis through pivot (roll, death, swim)."""
    Rp = R(axis, deg)
    c = Vector(pivot)
    P = T(c) @ Rp.to_4x4() @ T(-c)
    head = rig.rest["Hips"].translation
    new_off = c + Rp @ (head + hips_off - c) - head
    deltas = dict(deltas)
    deltas["Hips"] = Rp @ deltas.get("Hips", I3)
    return new_off, deltas, {sd: P @ F for sd, F in feet.items()}


def leg_swing(rig, side, deg, lift=0.0):
    """A foot placed by turning the whole leg about the hip joint (a Minecraft limb), raised by lift (the knee bends)."""
    H = rig.rest[side + "UpperLeg"].translation
    return T((0, 0, lift)) @ T(H) @ R(X, deg).to_4x4() @ T(-H)


def air_pose(rig, f):
    """In the air (jump and fall): one knee up, the other leg back, the arms a little out for balance."""
    p = (f - 1) / AIR_LEN
    w = math.sin(2 * math.pi * p)
    deltas = {}
    block_upper(rig, deltas, 4.0, 4.0, 0.0, 0.0, 25.0, 30.0)
    deltas["LeftUpperArm"] = R(X, -18.0 + 3.0 * w) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, 14.0 - 3.0 * w) @ deltas["RightUpperArm"]
    feet = {"Left": leg_swing(rig, "Left", -22.0 - 2.0 * w, 0.20), "Right": leg_swing(rig, "Right", 12.0 + 2.0 * w, 0.05)}
    return Vector((0.0, 0.0, 0.0)), deltas, feet


def land_pose(rig, f):
    """A hard landing: a quick squat that soaks up the fall, then straight back up."""
    p = (f - 1) / LAND_LEN
    s = smooth(p / 0.18) if p < 0.18 else (1.0 if p < 0.45 else 1.0 - smooth((p - 0.45) / 0.55))
    deltas = {"Hips": R(X, 4.0 * s)}
    block_upper(rig, deltas, 20.0 * s, 17.0 - 10.0 * s, 0.0, 0.0, 12.0 + 30.0 * s, 16.0 + 30.0 * s)
    deltas["LeftUpperArm"] = R(X, -25.0 * s) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, -25.0 * s) @ deltas["RightUpperArm"]
    return Vector((0.0, 0.0, -0.26 * s)), deltas, planted()


def roll_pose(rig, f):
    """Dodge roll: tuck, one forward somersault as a block, stand up."""
    p = (f - 1) / ROLL_LEN
    tuck = smooth(p / 0.15) if p < 0.8 else 1.0 - smooth((p - 0.8) / 0.2)
    turn = 360.0 * smooth((p - 0.12) / 0.68) if 0.12 < p < 0.8 else 0.0
    deltas = {"Hips": R(X, 10.0 * tuck)}
    block_upper(rig, deltas, 0.0, 25.0, 0.0, 0.0, 12.0 + 70.0 * tuck, 16.0 + 70.0 * tuck)
    deltas["Spine"] = R(X, 45.0 * tuck)
    deltas["Chest"] = R(X, 25.0 * tuck)
    deltas["Head"] = R(X, 30.0 * tuck)
    deltas["LeftUpperArm"] = R(X, -40.0 * tuck) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, -40.0 * tuck) @ deltas["RightUpperArm"]
    feet = {sd: T((0, -0.10 * tuck, 0.25 * tuck * tuck)) for sd in ("Left", "Right")}
    hips_off = Vector((0.0, 0.0, -0.48 * tuck))
    hips_off, deltas, feet = body_turn(hips_off, deltas, feet, rig, X, turn, (0.0, 0.0, 0.50))
    # rolls along the ground: lift the turned block so its lowest joint (plus the head and body thickness) stays on it
    if turn > 0.0:
        M, _ = rig.solve(hips_off, deltas, feet)
        low = min(min(m.translation.z, (m @ Vector((0, rig.length[n], 0))).z) for n, m in M.items()) - ROLL_SKIN
        if low < 0.0:
            hips_off = hips_off + Vector((0.0, 0.0, -low))
            feet = {sd: T((0, 0, -low)) @ F for sd, F in feet.items()}
    return hips_off, deltas, feet


def death_pose(rig, f):
    """Stiff, Minecraft-style: the body tips over sideways as one block and lands on its side."""
    p = (f - 1) / DEATH_LEN
    if p < 0.45:
        a = 90.0 * (p / 0.45) ** 2
    elif p < 0.6:
        a = 90.0 - 7.0 * math.sin(math.pi * (p - 0.45) / 0.15)
    else:
        a = 90.0
    deltas = {}
    block_upper(rig, deltas, 0.0, 38.0, 0.0, 0.0, 10.0, 14.0)
    return body_turn(Vector((0.0, 0.0, 0.0)), deltas, planted(), rig, Y, a, (0.46, 0.0, 0.0))


def swim_idle_pose(rig, f):
    """Treading water: upright, slow alternate leg kicks, the arms out sculling."""
    p = (f - 1) / SWIM_IDLE_LEN
    c1 = math.cos(2 * math.pi * p)
    deltas = {}
    block_upper(rig, deltas, 6.0, -15.0, 0.0, 0.0, 20.0, 25.0)
    deltas["LeftUpperArm"] = R(Z, 18.0 * c1) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(Z, 18.0 * c1) @ deltas["RightUpperArm"]
    feet = {"Left": leg_swing(rig, "Left", -16.0 * c1, 0.06 * max(0.0, c1)),
            "Right": leg_swing(rig, "Right", 16.0 * c1, 0.06 * max(0.0, -c1))}
    return Vector((0.0, 0.0, 0.0)), deltas, feet


def swim_pose(rig, f):
    """Swimming forward: the body lies along the stroke, flutter kick, the arms in a front crawl."""
    p = (f - 1) / SWIM_LEN
    deltas = {}
    block_upper(rig, deltas, 0.0, 30.0, 0.0, 0.0, 15.0, 20.0)
    deltas["Neck"] = R(X, -30.0)
    deltas["Head"] = R(X, -20.0)
    deltas["LeftUpperArm"] = R(X, 360.0 * p) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, 360.0 * p + 180.0) @ deltas["RightUpperArm"]
    k = math.sin(4 * math.pi * p)
    feet = {"Left": leg_swing(rig, "Left", 16.0 * k), "Right": leg_swing(rig, "Right", -16.0 * k)}
    return body_turn(Vector((0.0, 0.0, 0.0)), deltas, feet, rig, X, 75.0, (0.0, 0.0, 1.10))


def _combat_arms(rig, deltas, right_pitch, right_yaw, elbow):
    """Keep the shield arm in the walk guard. Drive the axe arm from the A-pose."""
    arm_base(rig, deltas, 18.0, 12.0, 20.0)
    ua = "RightUpperArm"
    deltas[ua] = R(toward(rig, ua, (0, 0, 1), 1), right_pitch) @ deltas[ua]
    deltas[ua] = R(Z, right_yaw) @ deltas[ua]
    deltas["RightLowerArm"] = R(toward(rig, "RightLowerArm", (0, -1, 0), 1), elbow) @ deltas["RightLowerArm"]


# One-handed axe chops in the Valheim manner (user, 2026-10-12: the sword slashes looked off): a wind-up like a
# throw (upper arm out to the side, forearm up, the axe head back behind the head), a short hold, a fast strike down
# and across with the body turning into it and the knees giving a little, a follow-through, then back to the guard.
# Each key: (time 0..1, upper arm raise from the A-pose, arm swing forward, elbow (+ folds), upper arm twist about its
# own length, wrist (- cocks the axe back), spine twist (+ turns right), chest pitch (+ leans forward), hips drop m). Between keys the values ease (smoothstep).
CHOP_KEYS = {
    # The wind-ups were found by a search over the rig (hand by the right shoulder, the axe head above and behind
    # the head); the strikes end low in front, across the body.
    "Attack_1": [(0.00, 0, 0, 0, 0, 0, 0, 0, 0.0),
                 (0.32, 80, -45, -60, -120, -80, 14, -8, 0.0),
                 (0.42, 85, -45, -60, -120, -85, 16, -10, 0.0),
                 (0.56, 10, 60, -5, 0, 0, -16, 16, 0.05),
                 (0.70, 5, 65, 0, 0, 5, -18, 18, 0.05),
                 (1.00, 0, 0, 0, 0, 0, 0, 0, 0.0)],
    "Attack_2": [(0.00, 0, 0, 0, 0, 0, 0, 0, 0.0),
                 (0.30, 135, 50, 15, 80, -45, -14, -6, 0.0),
                 (0.40, 135, 55, 15, 80, -45, -16, -8, 0.0),
                 (0.54, -15, -15, 10, 0, 0, 16, 12, 0.04),
                 (0.68, -20, -20, 12, 0, 5, 18, 14, 0.04),
                 (1.00, 0, 0, 0, 0, 0, 0, 0, 0.0)],
    "Attack_3": [(0.00, 0, 0, 0, 0, 0, 0, 0, 0.0),
                 (0.35, 155, 45, -60, -80, -30, 4, -14, -0.02),
                 (0.48, 160, 40, -60, -80, -35, 4, -16, -0.02),
                 (0.60, 0, 30, 5, 0, 0, -4, 24, 0.09),
                 (0.75, -3, 30, 8, 0, 5, -4, 24, 0.09),
                 (1.00, 0, 0, 0, 0, 0, 0, 0, 0.0)],
}


def chop_values(keys, p):
    for (t0, *a), (t1, *b) in zip(keys, keys[1:]):
        if p <= t1:
            s = smooth((p - t0) / max(1e-6, t1 - t0))
            return [x + (y - x) * s for x, y in zip(a, b)]
    return list(keys[-1][1:])


def _chop_arm(rig, deltas, raise_, swing, elbow, twist, wrist=0.0):
    """The shield arm in the walk guard; the axe arm raised out to the side, twisted about its own length, swung
    forward about the vertical, and the elbow folded."""
    arm_base(rig, deltas, 18.0, 12.0, 20.0)
    ua = "RightUpperArm"
    up_axis = toward(rig, ua, (0, 0, 1), 1)
    raised = R(up_axis, raise_) @ rig.rest_dir(ua)
    deltas[ua] = R(Z, swing) @ R(raised, twist) @ R(up_axis, raise_) @ deltas[ua]
    deltas["RightLowerArm"] = R(toward(rig, "RightLowerArm", (0, -1, 0), 1), elbow) @ deltas["RightLowerArm"]
    deltas["RightHand"] = R(toward(rig, "RightHand", (0, -1, 0), 1), wrist) @ deltas.get("RightHand", I3)


def chop_pose(name, length):
    def pose(rig, f):
        p = (f - 1) / length
        raise_, swing, elbow, twist, wrist, turn, lean, drop = chop_values(CHOP_KEYS[name], p)
        deltas = {}
        hips_off = Vector((0, 0, -drop))
        deltas["Spine"] = R(Z, turn * 0.5) @ R(X, lean * 0.5)
        deltas["Chest"] = R(Z, turn * 0.5) @ R(X, lean * 0.5)
        _chop_arm(rig, deltas, raise_, swing, elbow, twist, wrist)
        feet = {"Left": Matrix.Identity(4), "Right": Matrix.Identity(4)}
        return hips_off, deltas, feet
    return pose


# Fishing (user, 2026-10-12: the library cast looked like throwing the bobber by hand while holding the rod). The rod
# lies along Weapon_R's local +Z, the same axis as the axe head. Arm values were searched on the rig (the rod
# direction and the hand within a degree and a few cm): wait = rod forward 40 deg up, hand by the right hip; fight =
# rod 60 deg up, hand at the chest; release = rod forward and level, arm out; wind-up = rod back over the right
# shoulder. Same key layout as CHOP_KEYS.
FISH_WAIT = (57, 73, -105, 93, 0)
FISH_FIGHT = (100, 65, -157, 115, 25)
FISH_RELEASE = (80, 55, -102, 127, -60)
FISH_WINDUP = (65, -88, -115, -100, -55)
FISH_WAIT_LEN = 60     # loop: a slow rod-tip bob
FISH_CAST_LEN = 24     # one-shot, 0.8 s: back over the shoulder, whip forward, settle to the wait
FISH_FIGHT_LEN = 20    # loop: pumping the rod back against the fish
FISH_CATCH_LEN = 24    # one-shot: heave the rod up and back, then lower it to the carry


def _k(t, arm, turn=0.0, lean=0.0, drop=0.0):
    return (t, *arm, turn, lean, drop)


def _lerp_arm(a, b, s):
    return tuple(x + (y - x) * s for x, y in zip(a, b))


FISH_KEYS = {
    "FishWait": [_k(0.00, FISH_WAIT), _k(0.50, _lerp_arm(FISH_WAIT, FISH_FIGHT, 0.12)), _k(1.00, FISH_WAIT)],
    "FishCast": [_k(0.00, FISH_WAIT),
                 _k(0.30, FISH_WINDUP, turn=12, lean=-6),
                 _k(0.42, FISH_WINDUP, turn=14, lean=-8),
                 _k(0.56, FISH_RELEASE, turn=-8, lean=10, drop=0.03),
                 _k(0.70, FISH_RELEASE, turn=-8, lean=10, drop=0.03),
                 _k(1.00, FISH_WAIT)],
    "FishFight": [_k(0.00, FISH_FIGHT, lean=-6, drop=0.03),
                  _k(0.50, _lerp_arm(FISH_FIGHT, FISH_WINDUP, 0.25), lean=-10, drop=0.05),
                  _k(1.00, FISH_FIGHT, lean=-6, drop=0.03)],
    "FishCatch": [_k(0.00, FISH_FIGHT, lean=-6, drop=0.03),
                  _k(0.35, _lerp_arm(FISH_FIGHT, FISH_WINDUP, 0.6), turn=8, lean=-12, drop=0.04),
                  _k(0.50, _lerp_arm(FISH_FIGHT, FISH_WINDUP, 0.6), turn=8, lean=-12, drop=0.04),
                  _k(1.00, (0, 0, 0, 0, 0))],
}


def fish_pose(name, length):
    def pose(rig, f):
        p = (f - 1) / length
        raise_, swing, elbow, twist, wrist, turn, lean, drop = chop_values(FISH_KEYS[name], p)
        deltas = {}
        hips_off = Vector((0, 0, -drop))
        deltas["Spine"] = R(Z, turn * 0.5) @ R(X, lean * 0.5)
        deltas["Chest"] = R(Z, turn * 0.5) @ R(X, lean * 0.5)
        _chop_arm(rig, deltas, raise_, swing, elbow, twist, wrist)
        feet = {"Left": Matrix.Identity(4), "Right": Matrix.Identity(4)}
        return hips_off, deltas, feet
    return pose


# Gathering (open item after fishing: the library pick-up was the last motion capture on the Work layer): crouch and
# reach low in front of the right foot, close the hand, lift it to the chest, stand up. Reach values searched on the
# rig (hand 9 mm from a point 0.30 m above the ground, in front of the right toes, with the body bent 40 deg and
# lowered 0.30 m).
GATHER_REACH = (38, 90, -20, 5, 0)
GATHER_LEN = 30        # one-shot, 1.0 s
FISH_KEYS["Gather"] = [_k(0.00, (0, 0, 0, 0, 0)),
                       _k(0.38, GATHER_REACH, lean=40, drop=0.30),
                       _k(0.52, GATHER_REACH[:4] + (-25,), lean=40, drop=0.30),
                       _k(0.76, FISH_FIGHT[:2] + (FISH_FIGHT[2] + 20,) + FISH_FIGHT[3:], lean=8, drop=0.06),
                       _k(1.00, (0, 0, 0, 0, 0))]

fish_wait_pose = fish_pose("FishWait", FISH_WAIT_LEN)
fish_cast_pose = fish_pose("FishCast", FISH_CAST_LEN)
fish_fight_pose = fish_pose("FishFight", FISH_FIGHT_LEN)
fish_catch_pose = fish_pose("FishCatch", FISH_CATCH_LEN)
gather_pose = fish_pose("Gather", GATHER_LEN)

attack1_pose = chop_pose("Attack_1", ATTACK1_LEN)
attack2_pose = chop_pose("Attack_2", ATTACK2_LEN)
attack3_pose = chop_pose("Attack_3", ATTACK3_LEN)


def hit_pose(rig, f):
    p = (f - 1) / HIT_LEN
    s = math.sin(math.pi * p)
    deltas = {}
    hips_off = Vector((-0.02 * s, 0.03 * s, -0.04 * s))
    deltas["Hips"] = R(Z, 6 * s) @ R(X, -4 * s)
    deltas["Spine"] = R(X, -10 * s)
    deltas["Chest"] = R(X, -8 * s) @ R(Z, 8 * s)
    deltas["Head"] = R(Z, 10 * s) @ R(X, -6 * s)
    arm_base(rig, deltas, 16.0, 14.0, 30.0)
    feet = {"Left": Matrix.Identity(4), "Right": Matrix.Identity(4)}
    return hips_off, deltas, feet


# ------------------------------------------------------------------ keying
def key_action(rig, name, length, pose_fn, loop=True):
    ob = rig.ob
    ad = ob.animation_data_create()
    act = bpy.data.actions.get(name)
    if act:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    ad.action = act
    prev = {}
    for f in range(1, length + 2):
        hips_off, deltas, feet = pose_fn(rig, f)
        M, B = rig.solve(hips_off, deltas, feet)
        for sd in ("Left", "Right"):
            yv = M[sd + "UpperLeg"].to_3x3() @ Vector((0, 1, 0))
            ang = math.degrees(math.atan2(-yv.y, -yv.z))     # + = thigh forward (flexion)
            rig.thigh_range[0] = min(rig.thigh_range[0], ang)
            rig.thigh_range[1] = max(rig.thigh_range[1], ang)
        for pb in ob.pose.bones:
            if not pb.bone.use_deform:
                continue
            q = B[pb.name].to_quaternion()
            if pb.name in prev and prev[pb.name].dot(q) < 0:
                q.negate()
            prev[pb.name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
            if pb.name == "Hips":
                pb.location = B[pb.name].translation
                pb.keyframe_insert("location", frame=f, group=pb.name)
    # looping clips copy frame 1 onto the last frame so the seam is exact
    for fc in _fcurves(act):
        kps = fc.keyframe_points
        if loop:
            kps[-1].co.y = kps[0].co.y
        for kp in kps:
            kp.interpolation = "LINEAR"
        fc.update()
    track = ad.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, act)
    strip.name = name
    ad.action = None
    for pb in ob.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    return act


def _fcurves(act):
    try:
        return list(act.fcurves)
    except AttributeError:
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags:
                    out.extend(cb.fcurves)
        return out


def main():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.render.fps_base = 1.0
    ob = bpy.data.objects["chopper_rig"]
    if ob.animation_data:
        for tr in list(ob.animation_data.nla_tracks):
            ob.animation_data.nla_tracks.remove(tr)
    rig = Rig(ob, bpy.data.objects["chopper_boots"])
    # sanity: the zero pose must reproduce the rest pose
    M, B = rig.solve(Vector(), {}, {"Left": Matrix.Identity(4), "Right": Matrix.Identity(4)})
    err = max((B[n] - Matrix.Identity(4)).to_3x3().to_quaternion().angle if False else
              max(abs(B[n][i][j] - (1.0 if i == j else 0.0)) for i in range(4) for j in range(4))
              for n in B)
    print("rest reproduction max matrix error: %.2e" % err)
    clips = (
        ("Idle", IDLE_LEN, idle_pose, True),
        ("Walk", WALK_LEN, walk_pose, True),
        ("Run", RUN_LEN, run_pose, True),
        ("Sprint", SPRINT_LEN, sprint_pose, True),
        ("CrouchIdle", CROUCH_IDLE_LEN, crouch_idle_pose, True),
        ("CrouchWalk", CROUCH_LEN, crouch_walk_pose, True),
        ("Air", AIR_LEN, air_pose, True),
        ("LandHard", LAND_LEN, land_pose, False),
        ("Roll", ROLL_LEN, roll_pose, False),
        ("SwimIdle", SWIM_IDLE_LEN, swim_idle_pose, True),
        ("Swim", SWIM_LEN, swim_pose, True),
        ("Attack_1", ATTACK1_LEN, attack1_pose, False),
        ("Attack_2", ATTACK2_LEN, attack2_pose, False),
        ("Attack_3", ATTACK3_LEN, attack3_pose, False),
        ("HitReact", HIT_LEN, hit_pose, False),
        ("Death", DEATH_LEN, death_pose, False),
        ("FishWait", FISH_WAIT_LEN, fish_wait_pose, True),
        ("FishCast", FISH_CAST_LEN, fish_cast_pose, False),
        ("FishFight", FISH_FIGHT_LEN, fish_fight_pose, True),
        ("FishCatch", FISH_CATCH_LEN, fish_catch_pose, False),
        ("Gather", GATHER_LEN, gather_pose, False),
    )
    for name, length, fn, loop in clips:
        rig.max_reach = 0.0
        rig.max_loc_err = 0.0
        rig.thigh_range = [1e9, -1e9]
        key_action(rig, name, length, fn, loop=loop)
        print("%s: frames 1-%d loop=%s, max leg reach %.3f of full length, max non-root joint gap %.4f m, "
              "thigh angle %.1f..%.1f deg (+ = forward)"
              % (name, length + 1, loop, rig.max_reach, rig.max_loc_err, rig.thigh_range[0], rig.thigh_range[1]))
    sc.frame_start = 1
    sc.frame_end = IDLE_LEN + 1
    sc.frame_set(1)
    print("walk speed for in-place locomotion: %.2f m/s, cycle %.2f s, stride %.2f m"
          % (WALK_SPEED, WALK_LEN / FPS, WALK_SPEED * WALK_LEN / FPS))
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("saved", BLEND)


if __name__ == "__main__":
    main()
