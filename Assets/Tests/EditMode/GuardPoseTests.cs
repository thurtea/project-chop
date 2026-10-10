using Chopper.Player;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    // The procedural axe guard: at full blend the axe arm points where the pose says whatever the animation underneath
    // did, the axe handle slants up across the body, the left hand grips the handle, at zero blend nothing moves, and
    // the pose follows the way the character faces.
    public class GuardPoseTests
    {
        private GameObject _root;
        private GuardPose _pose;
        private Transform _upperL, _lowerL, _handL, _upperR, _lowerR, _handR, _spine3, _neck;
        private Transform _pelvis, _thighL, _calfL, _footL, _thighR, _calfR, _footR;
        private Transform _grip;

        [SetUp]
        public void BuildSkeleton()
        {
            // A player root (the frame), a model under it, and a small hanging skeleton with the rig's bone names.
            _root = new GameObject("Player");
            var model = new GameObject("Model");
            model.transform.SetParent(_root.transform, false);
            Transform pelvis = Bone("pelvis", model.transform, new Vector3(0f, 0.95f, 0f));
            _pelvis = pelvis;
            // Straight legs hanging from the hips, as long as the rig's (0.40 and 0.43 m).
            _thighL = Bone("thigh_l", pelvis, new Vector3(-0.09f, 0f, 0f));
            _calfL = Bone("calf_l", _thighL, new Vector3(0f, -0.40f, 0f));
            _footL = Bone("foot_l", _calfL, new Vector3(0f, -0.43f, 0f));
            _thighR = Bone("thigh_r", pelvis, new Vector3(0.09f, 0f, 0f));
            _calfR = Bone("calf_r", _thighR, new Vector3(0f, -0.40f, 0f));
            _footR = Bone("foot_r", _calfR, new Vector3(0f, -0.43f, 0f));
            Transform spine1 = Bone("spine_01", pelvis, new Vector3(0f, 0.14f, 0f));
            Transform spine2 = Bone("spine_02", spine1, new Vector3(0f, 0.12f, 0f));
            _spine3 = Bone("spine_03", spine2, new Vector3(0f, 0.14f, 0f));
            _neck = Bone("neck_01", _spine3, new Vector3(0f, 0.17f, 0f));
            Bone("Head", _neck, new Vector3(0f, 0.08f, 0f));
            // Arms hang straight down, which is not the pose the guard wants.
            _upperL = Bone("upperarm_l", _spine3, new Vector3(-0.2f, 0.13f, 0f));
            _lowerL = Bone("lowerarm_l", _upperL, new Vector3(0f, -0.27f, 0f));
            _handL = Bone("hand_l", _lowerL, new Vector3(0f, -0.27f, 0f));
            _upperR = Bone("upperarm_r", _spine3, new Vector3(0.2f, 0.13f, 0f));
            _lowerR = Bone("lowerarm_r", _upperR, new Vector3(0f, -0.27f, 0f));
            _handR = Bone("hand_r", _lowerR, new Vector3(0f, -0.27f, 0f));
            // The axe in the right hand: a grip at the hand and a 0.8 m handle hanging down from it.
            Transform grip = Bone("AxeGrip", _handR, Vector3.zero);
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.transform.SetParent(grip, false);
            handle.transform.localScale = new Vector3(0.04f, 0.8f, 0.04f);
            handle.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            _grip = grip;
            _pose = model.AddComponent<GuardPose>();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_root);
        }

        private static Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            return bone;
        }

        // The direction from a bone to its child, in the player root's own frame (x right, y up, z forward).
        private Vector3 InFrame(Transform from, Transform to)
        {
            Vector3 world = (to.position - from.position).normalized;
            Transform frame = _root.transform;
            return new Vector3(Vector3.Dot(world, frame.right), Vector3.Dot(world, frame.up), Vector3.Dot(world, frame.forward));
        }

        [Test]
        public void FullBlendPointsTheArmsWhereThePoseSays()
        {
            _pose.Evaluate(1f);
            Assert.That(Vector3.Angle(InFrame(_upperR, _lowerR), GuardPose.AxeUpperArm), Is.LessThan(1f));
            Assert.That(Vector3.Angle(InFrame(_lowerR, _handR), GuardPose.AxeForearm), Is.LessThan(1f));
        }

        [Test]
        public void TheHandleSlantsUpAcrossTheBody()
        {
            _pose.Evaluate(1f);
            Vector3 handle = _root.transform.InverseTransformDirection(_pose.AxeHandleDirection());
            Assert.That(Vector3.Angle(handle, GuardPose.AxeHandle.normalized), Is.LessThan(2f));
        }

        [Test]
        public void TheLeftHandGripsTheHandle()
        {
            _pose.Evaluate(1f);
            Vector3 grip = _grip.position + _pose.AxeHandleDirection() * GuardPose.LeftGripAlongHandle;
            Assert.That(Vector3.Distance(_handL.position, grip), Is.LessThan(0.03f), "the left hand is on the handle");
        }

        [Test]
        public void ZeroBlendLeavesTheAnimationAlone()
        {
            Quaternion before = _upperL.rotation;
            Vector3 handBefore = _handL.position;
            _pose.Evaluate(0f);
            Assert.That(Quaternion.Angle(before, _upperL.rotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(handBefore, _handL.position), Is.LessThan(0.0001f));
        }

        [Test]
        public void HalfBlendIsBetweenTheAnimationAndTheGuard()
        {
            Vector3 hanging = InFrame(_upperR, _lowerR);
            _pose.Evaluate(0.5f);
            float half = Vector3.Angle(hanging, InFrame(_upperR, _lowerR));
            _pose.Evaluate(1f);
            float full = Vector3.Angle(hanging, InFrame(_upperR, _lowerR));
            Assert.That(half, Is.GreaterThan(1f));
            Assert.That(half, Is.LessThan(full));
        }

        [Test]
        public void ThePoseFollowsWhichWayTheCharacterFaces()
        {
            _root.transform.rotation = Quaternion.Euler(0f, 130f, 0f);
            _pose.Evaluate(1f);
            Assert.That(Vector3.Angle(InFrame(_upperR, _lowerR), GuardPose.AxeUpperArm), Is.LessThan(1f));
            Assert.That(Vector3.Angle(InFrame(_lowerR, _handR), GuardPose.AxeForearm), Is.LessThan(1f));
        }

        [Test]
        public void TheAxeHandEndsInFrontOfTheChest()
        {
            _pose.Evaluate(1f);
            Vector3 hand = _root.transform.InverseTransformPoint(_handR.position);
            Vector3 shoulder = _root.transform.InverseTransformPoint(_upperR.position);
            Assert.Greater(hand.z - shoulder.z, 0.2f, "the axe hand is out in front of the shoulder");
            Assert.Greater(hand.y, shoulder.y - 0.5f, "and not hanging at the hip");
        }

        [Test]
        public void TheHipsSinkAndTheFeetStayPlanted()
        {
            Vector3 leftFoot = _footL.position;
            Vector3 rightFoot = _footR.position;
            float hipHeight = _pelvis.position.y;
            _pose.Evaluate(1f);
            Assert.That(hipHeight - _pelvis.position.y, Is.EqualTo(GuardPose.CrouchDrop).Within(0.002f), "the hips sink by the crouch depth");
            Assert.That(Vector3.Distance(leftFoot, _footL.position), Is.LessThan(0.01f), "left foot stays put");
            Assert.That(Vector3.Distance(rightFoot, _footR.position), Is.LessThan(0.01f), "right foot stays put");
        }

        [Test]
        public void TheKneesBendForward()
        {
            _pose.Evaluate(1f);
            Transform frame = _root.transform;
            Vector3 hip = frame.InverseTransformPoint(_thighL.position);
            Vector3 knee = frame.InverseTransformPoint(_calfL.position);
            Vector3 foot = frame.InverseTransformPoint(_footL.position);
            float lineZ = Mathf.Lerp(hip.z, foot.z, (hip.y - knee.y) / (hip.y - foot.y));
            Assert.Greater(knee.z - lineZ, 0.01f, "the knee sits in front of the hip to foot line");
        }

        [Test]
        public void TheCrouchFadesInWithTheBlend()
        {
            float hipHeight = _pelvis.position.y;
            _pose.Evaluate(0.5f);
            float half = hipHeight - _pelvis.position.y;
            Assert.That(half, Is.EqualTo(GuardPose.CrouchDrop * 0.5f).Within(0.002f));
        }

        [Test]
        public void TheTorsoLeansForwardByTheSetAmountWhateverItStartedAs()
        {
            // Start with the spine tipped backward, as a different animation might have it.
            _spine3.parent.rotation = Quaternion.AngleAxis(-25f, _root.transform.right) * _spine3.parent.rotation;
            _pose.Evaluate(1f);
            Vector3 torso = InFrame(_spine3, _neck);
            float lean = Mathf.Atan2(torso.z, torso.y) * Mathf.Rad2Deg;
            Assert.That(lean, Is.InRange(3f, 14f), "a slight forward lean, not the animation's lean");
        }
    }
}
