using MiddleMousePan.Settings;
using HarmonyLib;
using Kingmaker.Controllers.Clicks;
using Kingmaker.View;
using System.Reflection;
using UnityEngine;

namespace MiddleMousePan.Features.Camera
{
    /// <summary>
    /// Hold Middle Mouse Button to pan/move the camera by dragging.
    /// - Accumulates MMB drag into a world-space delta
    /// - Blocks vanilla MMB-rotate while held
    /// - Applies movement via our own LateUpdate MonoBehaviour (no reliance on CameraRig methods)
    /// - Tries to nudge CameraRig internal state (if fields found) to avoid snap-back
    /// </summary>
    public class MoveCameraWithMMB : ModToggleSettingEntry
    {
        private const string _key = "movecamerawithmmb";
        private const string _title = "Move camera with middle mouse button";
        private const string _tooltip = "Hold middle mouse button and drag to pan the camera.";
        private const bool _defaultValue = true;

        public MoveCameraWithMMB() : base(_key, _title, _tooltip, _defaultValue) { }

        private static bool s_enabled;

        public override SettingStatus TryEnable()
        {
            s_enabled = SettingEntity.GetValue();
            PanApplier.Ensure(); // make sure our helper exists
            return base.TryEnable();
        }

        // ================== INPUT HOOK (reliable Postfix) ==================
        [HarmonyPatch(typeof(PointerController), nameof(PointerController.Tick))]
        private static class PointerTickPostfixPatch
        {
            [HarmonyPostfix]
            private static void PostfixCallAccumulator()
            {
                AccumulatePanFromMMB();
            }
        }

        private static Vector2 s_lastMouse;
        private static bool s_dragging;
        private static Vector3 s_pendingWorldPan; // consumed by PanApplier
        private static object s_prevFollow;

        private static bool IsAltDown()
        {
            // AltGr usually maps to RightAlt on Windows, so these two are enough.
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private static void AccumulatePanFromMMB()
        {
            if (!s_enabled) { s_dragging = false; s_pendingWorldPan = Vector3.zero; return; }

            var rig = CameraRig.Instance;   // EINMAL definieren

            if (Input.GetMouseButtonDown(2))
            {
                if (IsAltDown())
                {
                    if (s_dragging)
                    {
                        s_dragging = false;
                        if (rig != null && _fiFollowTarget != null && s_prevFollow != null)
                        {
                            _fiFollowTarget.SetValue(rig, s_prevFollow);
                            s_prevFollow = null;
                        }
                    }
                    return; // vanilla rotate
                }

                PanApplier.Ensure();
                s_dragging = true;
                s_lastMouse = Input.mousePosition;

                EnsureFieldCache(rig?.GetType());
                if (rig != null && _fiFollowTarget != null)
                {
                    s_prevFollow = _fiFollowTarget.GetValue(rig);
                    _fiFollowTarget.SetValue(rig, null);
                }

                Debug.Log("[MoveCameraWithMMB] MMB down");
            }

            if (s_dragging && IsAltDown())
            {
                s_dragging = false;
                if (rig != null && _fiFollowTarget != null)
                {
                    _fiFollowTarget.SetValue(rig, s_prevFollow);
                    s_prevFollow = null;
                }
                return;
            }

            if (s_dragging && Input.GetMouseButton(2))
            {
                var cur = (Vector2)Input.mousePosition;
                var deltaPix = cur - s_lastMouse;
                s_lastMouse = cur;

                if (deltaPix.sqrMagnitude > 0f)
                {
                    var cam = (rig?.Camera != null) ? rig.Camera : UnityEngine.Camera.main;
                    if (cam != null)
                    {
                        const float PanPixelsToUnits = 0.02f;
                        const float PanSpeed = 1.5f;

                        Vector3 right = cam.transform.right;
                        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;

                        Vector3 world = (-right * deltaPix.x + -fwd * deltaPix.y) * PanPixelsToUnits * PanSpeed;
                        s_pendingWorldPan += world;

                        Debug.Log($"[MoveCameraWithMMB] Accumulate worldΔ={world} (pixΔ={deltaPix})");
                    }
                }
            }

            if (Input.GetMouseButtonUp(2))
            {
                s_dragging = false;

                if (rig != null && _fiFollowTarget != null)
                {
                    _fiFollowTarget.SetValue(rig, s_prevFollow);
                    s_prevFollow = null;
                }
                Debug.Log("[MoveCameraWithMMB] MMB up");
            }
        }


        // ================== BLOCK VANILLA MMB ROTATE ==================
        [HarmonyPatch]
        private static class BlockDefaultMMBRotate
        {
            [HarmonyPatch(typeof(CameraRig), nameof(CameraRig.RotateByMiddleButton))]
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static bool PreventRotateByMMB()
            {
                // Block rotate only for plain MMB; allow it when Alt is held
                if (s_enabled && (Input.GetMouseButton(2) || Input.GetMouseButtonDown(2)) && !IsAltDown())
                    return false; // skip original (block)
                return true;       // run original (allow)
            }

            [HarmonyPatch(typeof(CameraRig), nameof(CameraRig.CheckRotate))]
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static bool PreventCheckRotate()
            {
                if (s_enabled && (Input.GetMouseButton(2) || Input.GetMouseButtonDown(2)) && !IsAltDown())
                    return false;
                return true;
            }
        }

        // ================== PAN APPLIER (our own MonoBehaviour) ==================
        private class PanApplier : MonoBehaviour
        {
            private static PanApplier _instance;

            public static void Ensure()
            {
                if (_instance != null) return;
                var go = new GameObject("MiddleMousePan_MoveCameraWithMMB");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PanApplier>();
                Debug.Log("[MoveCameraWithMMB] PanApplier created");
            }

            private void LateUpdate()
            {
                if (!s_enabled) return;

                var delta = s_pendingWorldPan;
                if (delta == Vector3.zero) return;

                var rig = CameraRig.Instance;
                if (rig == null)
                {
                    // last resort: move main camera (visual), then clear
                    var cam = UnityEngine.Camera.main;
                    if (cam != null) cam.transform.position += delta;
                    Debug.Log("[MoveCameraWithMMB] Apply(LateUpdate) without rig; moved main camera only");
                    s_pendingWorldPan = Vector3.zero;
                    return;
                }

                // Try to nudge internal state first (prevents snap-back)
                EnsureFieldCache(rig.GetType());
                bool touched = false;
                touched |= TryAddToVector3Field(rig, _fiCurrentPos, delta);
                touched |= TryAddToVector3Field(rig, _fiTargetPos, delta);
                touched |= TryAddToVector3Field(rig, _fiPivot, delta);
                touched |= TryAddToVector3Field(rig, _fiLookAt, delta);

                // Always move the transform too, at the end of the frame
                rig.transform.position += delta;

                Debug.Log($"[MoveCameraWithMMB] Apply(LateUpdate) worldΔ={delta}, touchedState={touched}");
                s_pendingWorldPan = Vector3.zero;
            }
        }

        // ================== reflection helpers ==================
        private static FieldInfo _fiCurrentPos, _fiTargetPos, _fiPivot, _fiLookAt, _fiFollowTarget;
        private static bool _fieldsResolved;

        private static void EnsureFieldCache(System.Type rigType)
        {
            if (_fieldsResolved || rigType == null) return;

            _fiCurrentPos = FindVec3(rigType, "m_CurrentPosition", "m_CurrentPos", "m_Position", "m_CameraPosition");
            _fiTargetPos = FindVec3(rigType, "m_TargetPosition", "m_DesiredPosition", "m_TargetPos");
            _fiPivot = FindVec3(rigType, "Pivot", "m_Pivot", "m_PivotPoint");
            _fiLookAt = FindVec3(rigType, "m_LookAtPoint", "LookAtPoint", "m_Target");
            _fiFollowTarget = AccessTools.Field(rigType, "m_FollowTarget") ?? AccessTools.Field(rigType, "FollowTarget");

            _fieldsResolved = true;
            try { Main.log.Log($"[MoveCameraWithMMB] Fields: cur={_fiCurrentPos != null}, tgt={_fiTargetPos != null}, piv={_fiPivot != null}, look={_fiLookAt != null}, follow={_fiFollowTarget != null}"); } catch { }
        }

        private static FieldInfo FindVec3(System.Type t, params string[] names)
        {
            foreach (var n in names)
            {
                var fi = AccessTools.Field(t, n);
                if (fi != null && fi.FieldType == typeof(Vector3)) return fi;
            }
            return null;
        }

        private static bool TryAddToVector3Field(object inst, FieldInfo fi, Vector3 add)
        {
            if (fi == null) return false;
            var v = (Vector3)fi.GetValue(inst);
            fi.SetValue(inst, v + add);
            return true;
        }
    }
}
