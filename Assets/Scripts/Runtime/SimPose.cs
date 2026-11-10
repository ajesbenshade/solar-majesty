using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// A body that moves on the fixed simulation step and is drawn between steps. The transform
    /// holds the simulated position while steps run, and a blend of the last two simulated
    /// positions while the frame is drawn, so a 20 Hz step still reads as smooth travel.
    /// <para>
    /// Each frame the game loop calls <see cref="Begin"/> on every body, runs the steps (each body
    /// reports its step with <see cref="Stepped"/>), then calls <see cref="Present"/>.
    /// </para>
    /// </summary>
    public struct SimPose
    {
        private Vector3 _prev;
        private Vector3 _cur;
        private Vector3 _shown;
        private bool _gliding;

        /// <summary>True while the transform is being drawn between two simulated positions.</summary>
        public bool Gliding => _gliding;

        /// <summary>
        /// Before the frame's commands and steps: put the transform back on its simulated
        /// position. If something outside the simulation moved the body since it was last drawn
        /// (a spawn, a load, a recall), that move is kept and becomes the simulated position.
        /// </summary>
        public void Begin(Transform t)
        {
            if (!_gliding) return;
            Vector3 now = t.position;
            float dx = now.x - _shown.x;
            float dz = now.z - _shown.z;
            // Compared on the plane: per-frame dressing may bob the body up and down.
            if (dx * dx + dz * dz > 1e-6f)
            {
                _gliding = false;
                return;
            }
            t.position = _cur;
        }

        /// <summary>
        /// After one simulation step. <paramref name="before"/> is where the body stood when the
        /// step began. A step that did not move the transform leaves it alone: either the body is
        /// standing still, or something else (a NavMeshAgent) is carrying it frame by frame.
        /// </summary>
        public void Stepped(Transform t, Vector3 before)
        {
            Vector3 now = t.position;
            _gliding = (now - before).sqrMagnitude > 1e-10f;
            _prev = before;
            _cur = now;
        }

        /// <summary>After the frame's steps: draw the body <paramref name="alpha"/> of the way through its last step.</summary>
        public void Present(Transform t, float alpha)
        {
            if (!_gliding) return;
            _shown = Vector3.LerpUnclamped(_prev, _cur, Mathf.Clamp01(alpha));
            t.position = _shown;
        }
    }
}
