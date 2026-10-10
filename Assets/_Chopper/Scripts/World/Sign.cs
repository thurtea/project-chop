using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // A sign in the world. Its text is not drawn on the sign: walk up and press interact to read it in a panel. The
    // signpost by the hut holds the guide (the How to play text, with the player's own controls). Signs players put up
    // and write on themselves will use the same component through SetText once building pieces exist.
    public class Sign : MonoBehaviour
    {
        [SerializeField] private string _title = "Sign";
        [SerializeField, TextArea(3, 12)] private string _body = string.Empty;
        [Tooltip("Shows the How to play guide instead of the body text.")]
        [SerializeField] private bool _guide;
        [SerializeField, Min(0.5f)] private float _reach = 2.6f;

        private static readonly List<Sign> _all = new List<Sign>();

        public static IReadOnlyList<Sign> All => _all;

        public string Title => _title;
        public string Body => _body;
        public bool IsGuide => _guide;

        // The signpost with the guide, by the hut.
        public static Sign Guide
        {
            get
            {
                foreach (Sign sign in _all) if (sign != null && sign._guide) return sign;
                return null;
            }
        }

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);

        public void Configure(string title, string body, bool guide)
        {
            _title = title;
            _body = body;
            _guide = guide;
        }

        // For signs players write on (later).
        public void SetText(string title, string body)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Sign" : title.Trim();
            _body = body ?? string.Empty;
        }

        public bool IsInReach(Vector3 position)
        {
            Vector3 offset = transform.position - position;
            if (Mathf.Abs(offset.y) > 3f) return false;
            offset.y = 0f;
            return offset.magnitude <= _reach;
        }

        // The closest sign within reach, or null.
        public static Sign Nearest(Vector3 position)
        {
            Sign best = null;
            float bestSqr = float.MaxValue;
            foreach (Sign sign in _all)
            {
                if (sign == null || !sign.IsInReach(position)) continue;
                float sqr = (sign.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = sign;
                }
            }
            return best;
        }
    }
}
