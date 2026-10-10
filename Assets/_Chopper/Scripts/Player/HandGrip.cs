using UnityEngine;

namespace Chopper.Player
{
    // Where a held handle goes: the hollow of the closed fist, found from the finger bones as they are this frame (the
    // animation curls the fingers, so the bind pose cannot say where the fist is). The hollow sits among the knuckles
    // of the index and little fingers, the curled middle finger and the thumb. Also the line across the fist (little
    // finger to index), which is the way a handle runs through it.
    public sealed class HandGrip
    {
        private readonly Transform _hand;
        private readonly Transform _index;
        private readonly Transform _pinky;
        private readonly Transform _middleCurl;
        private readonly Transform _middleTip;
        private readonly Transform _thumb;

        public HandGrip(Transform hand, string side)
        {
            _hand = hand;
            if (hand == null) return;
            _index = Find(hand, "index_01_" + side);
            _pinky = Find(hand, "pinky_01_" + side);
            _middleCurl = Find(hand, "middle_02_" + side);
            _middleTip = Find(hand, "middle_03_" + side);
            _thumb = Find(hand, "thumb_02_" + side);
        }

        public bool Valid => _hand != null && _index != null && _pinky != null && _middleCurl != null && _thumb != null;

        // World position of the hollow of the fist.
        public Vector3 Center
        {
            get
            {
                if (!Valid) return _hand != null ? _hand.position : Vector3.zero;
                Vector3 sum = _index.position + _pinky.position + _middleCurl.position + _thumb.position;
                int count = 4;
                if (_middleTip != null)
                {
                    sum += _middleTip.position;
                    count++;
                }
                return sum / count;
            }
        }

        // World direction a handle runs through the fist: from the little finger side to the index side.
        public Vector3 Across => Valid ? (_index.position - _pinky.position).normalized : (_hand != null ? _hand.right : Vector3.right);

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
