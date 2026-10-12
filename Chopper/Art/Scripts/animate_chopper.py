"""Author Idle, Walk, Run, Attack_1, Attack_2, Attack_3, HitReact and Death on chopper_rig.

Run:  blender -b -P animate_chopper.py
In:   Art/Blender/chopper_rigged.blend (from rig_chopper.py)
Out:  same file, one NLA strip per action (one FBX take per strip).

Idle, Walk and Run loop (first frame copied onto the last). The four combat clips
are one-shots: the last frame is the end pose, not a copy of the first.

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
RUN_LEN = 20           # frames 1..21
RUN_SPEED = 2.20       # m/s
ATTACK1_LEN = 22       # diagonal chop from over the right shoulder, one-shot
ATTACK2_LEN = 22       # diagonal chop from over the left shoulder, one-shot
ATTACK3_LEN = 28       # overhead finisher, one-shot
HIT_LEN = 15
DEATH_LEN = 36
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


def idle_pose(rig, f):
    p = (f - 1) / IDLE_LEN
    br = math.sin(2 * math.pi * p)                 # breath
    sh = math.sin(2 * math.pi * p + 0.6)           # weight shift
    deltas = {}
    hips_off = Vector((0.012 * sh, 0.0, -0.016 - 0.003 * math.cos(4 * math.pi * p)))
    deltas["Hips"] = R(Y, -1.4 * sh)
    deltas["Spine"] = R(Y, 0.9 * sh) @ R(X, 1.0)
    deltas["Chest"] = R(Y, 0.5 * sh) @ R(X, -0.9 * br)
    deltas["UpperChest"] = R(X, -0.9 * br)
    deltas["Neck"] = R(X, 0.9 * br - 0.5)
    deltas["Head"] = R(X, 0.6 * br) @ R(Z, 2.0 * math.sin(2 * math.pi * p + 1.3))
    # shoulders rise on the in-breath
    for side in ("Left", "Right"):
        nm = side + "Shoulder"
        deltas[nm] = R(toward(rig, nm, (0, 0, 1), 1), 1.8 * br)
    arm_base(rig, deltas, 21.0, 12.0, 38.0)
    for side, ph in (("Left", 0.0), ("Right", 0.4)):
        ua = side + "UpperArm"
        deltas[ua] = R(X, 1.2 * math.sin(2 * math.pi * p + ph)) @ deltas[ua]
        deltas[ua] = R(toward(rig, ua, (0, 0, -1), 1), -0.8 * br) @ deltas[ua]
    feet = {"Left": Matrix.Identity(4), "Right": Matrix.Identity(4)}
    return hips_off, deltas, feet


def stance_F(rig, side, q, y0, speed=WALK_SPEED, length=WALK_LEN):
    """Planted foot at local phase q in [0, STANCE]; pivot heel then flat then toe."""
    g = T((0, y0 + speed * q * length / FPS, 0))
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


def foot_F(rig, side, q, y0, speed=WALK_SPEED, length=WALK_LEN, lift=SWING_LIFT):
    """Foot transform at local phase q. Stance: ground-attached roll. Swing: the foot pitch
    goes from push-off to flat by mid-swing and to the heel-strike pitch at the end; its
    height is set so the lowest boot vertex follows a low clearance arc."""
    q = q % 1.0
    if q <= STANCE:
        return stance_F(rig, side, q, y0, speed, length), True
    s = (q - STANCE) / (1 - STANCE)
    F0 = stance_F(rig, side, STANCE, y0, speed, length)
    F1 = stance_F(rig, side, 0.0, y0, speed, length)
    ank = rig.rest[side + "Foot"].translation
    A0, A1 = F0 @ ank, F1 @ ank
    dur = (1 - STANCE) * length / FPS
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
    key = (pose_fn.__name__, length, STANCE, FOOT_SHIFT, REACH_TARGET, REACH_MAX, PELVIS_HARMONICS)
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
    c1 = math.cos(2 * math.pi * p)
    s1 = math.sin(2 * math.pi * p)
    if pelvis_z is None:
        pelvis_z = pelvis_heights(rig)[(f - 1) % WALK_LEN]
    hips_off = Vector((0.014 * math.sin(2 * math.pi * (p - 0.05)), 0.0, pelvis_z))
    deltas = {}
    deltas["Hips"] = R(Z, -5.0 * c1) @ R(Y, -2.5 * math.sin(2 * math.pi * (p - 0.05))) @ R(X, 3.0)
    deltas["Spine"] = R(Z, 2.5 * c1) @ R(Y, 1.5 * math.sin(2 * math.pi * (p - 0.05))) @ R(X, 1.5)
    deltas["Chest"] = R(Z, 3.0 * c1) @ R(X, 0.8 * math.cos(4 * math.pi * p))
    deltas["UpperChest"] = R(Z, 1.5 * c1)
    deltas["Neck"] = R(X, -3.0) @ R(Z, -1.0 * c1)
    deltas["Head"] = R(X, -1.5 - 0.8 * math.cos(4 * math.pi * p)) @ R(Z, -1.0 * c1)
    for side in ("Left", "Right"):
        nm = side + "Shoulder"
        deltas[nm] = R(toward(rig, nm, (0, 0, 1), 1), -1.0)
    # arms: relaxed base, swing opposite to the legs (left leg forward at p=0 -> left arm back)
    swing_l = 17.0 * c1
    swing_r = -17.0 * c1
    # shield arm: elbow held steady (the shield extends past the elbow; extra flexion would
    # sweep its curved top rim across the upper-arm sleeve)
    el_l = 12.0
    el_r = 14.0 + 10.0 * max(0.0, c1)
    arm_base(rig, deltas, 15.0, el_l, el_r)
    deltas["LeftUpperArm"] = R(X, swing_l) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, swing_r) @ deltas["RightUpperArm"]
    # feet: left heel strike at p=0, right at p=0.5
    y0 = -WALK_SPEED * STANCE * WALK_LEN / FPS * 0.5 + FOOT_SHIFT
    FL, pl = foot_F(rig, "Left", p, y0)
    FR, pr = foot_F(rig, "Right", p + 0.5, y0)
    return hips_off, deltas, {"Left": FL, "Right": FR}


def gait_pose(rig, f, length, speed, pelvis_fn, adduct, swing, elbow_l, elbow_r, lean, lift):
    p = (f - 1) / length
    c1 = math.cos(2 * math.pi * p)
    if pelvis_fn is None:
        pelvis_z = 0.0
    else:
        pelvis_z = pelvis_fn(rig)[(f - 1) % length]
    hips_off = Vector((0.018 * math.sin(2 * math.pi * (p - 0.05)), 0.0, pelvis_z))
    deltas = {}
    deltas["Hips"] = R(Z, -6.0 * c1) @ R(X, lean)
    deltas["Spine"] = R(Z, 3.0 * c1) @ R(X, lean * 0.45)
    deltas["Chest"] = R(Z, 3.5 * c1)
    deltas["UpperChest"] = R(Z, 1.5 * c1)
    deltas["Neck"] = R(X, -4.0) @ R(Z, -1.0 * c1)
    deltas["Head"] = R(X, -2.0) @ R(Z, -0.8 * c1)
    for side in ("Left", "Right"):
        nm = side + "Shoulder"
        deltas[nm] = R(toward(rig, nm, (0, 0, 1), 1), -1.0)
    arm_base(rig, deltas, adduct, elbow_l, elbow_r + 8.0 * max(0.0, c1))
    deltas["LeftUpperArm"] = R(X, swing * c1) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(X, -swing * c1) @ deltas["RightUpperArm"]
    y0 = -speed * STANCE * length / FPS * 0.5 + FOOT_SHIFT
    FL, _pl = foot_F(rig, "Left", p, y0, speed, length, lift)
    FR, _pr = foot_F(rig, "Right", p + 0.5, y0, speed, length, lift)
    return hips_off, deltas, {"Left": FL, "Right": FR}


def run_pose(rig, f, pelvis_z=None):
    if pelvis_z is not None:
        # used while solving the pelvis curve: feet still move, hips stay at the probe height
        p = (f - 1) / RUN_LEN
        c1 = math.cos(2 * math.pi * p)
        deltas = {}
        deltas["Hips"] = R(X, 12.0)
        arm_base(rig, deltas, 12.0, 12.0, 20.0)
        y0 = -RUN_SPEED * STANCE * RUN_LEN / FPS * 0.5
        FL, _pl = foot_F(rig, "Left", p, y0, RUN_SPEED, RUN_LEN, 0.09)
        FR, _pr = foot_F(rig, "Right", p + 0.5, y0, RUN_SPEED, RUN_LEN, 0.09)
        return Vector((0, 0, pelvis_z)), deltas, {"Left": FL, "Right": FR}
    return gait_pose(rig, f, RUN_LEN, RUN_SPEED, lambda r: pelvis_heights(r, run_pose, RUN_LEN),
                     12.0, 28.0, 12.0, 18.0, 12.0, 0.09)


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
                 (0.32, 95, -45, -60, -120, -60, 14, -8, 0.0),
                 (0.42, 100, -45, -60, -120, -65, 16, -10, 0.0),
                 (0.56, -20, 60, 15, 0, 0, -16, 16, 0.05),
                 (0.70, -25, 65, 20, 0, 5, -18, 18, 0.05),
                 (1.00, 0, 0, 0, 0, 0, 0, 0, 0.0)],
    "Attack_2": [(0.00, 0, 0, 0, 0, 0, 0, 0, 0.0),
                 (0.30, 150, 30, 15, 80, -60, -14, -6, 0.0),
                 (0.40, 150, 35, 15, 80, -65, -16, -8, 0.0),
                 (0.54, -15, -15, 10, 0, 0, 16, 12, 0.04),
                 (0.68, -20, -20, 12, 0, 5, 18, 14, 0.04),
                 (1.00, 0, 0, 0, 0, 0, 0, 0, 0.0)],
    "Attack_3": [(0.00, 0, 0, 0, 0, 0, 0, 0, 0.0),
                 (0.35, 155, 45, -60, -80, -30, 4, -14, -0.02),
                 (0.48, 160, 40, -60, -80, -35, 4, -16, -0.02),
                 (0.60, -30, 30, 5, 0, 0, -4, 24, 0.09),
                 (0.75, -33, 30, 8, 0, 5, -4, 24, 0.09),
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


def death_pose(rig, f):
    p = (f - 1) / DEATH_LEN
    s = smooth(min(1.0, p / 0.75))
    deltas = {}
    hips_off = Vector((0.0, 0.05 * s, -0.42 * s))
    deltas["Hips"] = R(X, -55 * s)
    deltas["Spine"] = R(X, -25 * s)
    deltas["Chest"] = R(X, -15 * s)
    deltas["Head"] = R(X, 20 * s)
    arm_base(rig, deltas, 10.0, 20.0, 25.0)
    deltas["LeftUpperArm"] = R(Z, 30 * s) @ deltas["LeftUpperArm"]
    deltas["RightUpperArm"] = R(Z, -40 * s) @ deltas["RightUpperArm"]
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
        ("Attack_1", ATTACK1_LEN, attack1_pose, False),
        ("Attack_2", ATTACK2_LEN, attack2_pose, False),
        ("Attack_3", ATTACK3_LEN, attack3_pose, False),
        ("HitReact", HIT_LEN, hit_pose, False),
        ("Death", DEATH_LEN, death_pose, False),
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
