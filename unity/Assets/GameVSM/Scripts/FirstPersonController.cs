using UnityEngine;
using UnityEngine.InputSystem;

namespace GameVSM
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public Camera View;
        public bool MenuOpen = true;
        public Vector2 TouchMove;
        public Vector2 TouchLook;
        public TrainMechanism Target { get; private set; }
        public string Hint => experience != null && experience.CanFocus ? experience.TargetLabel + "   " + InputHints.Action(experience.TargetAction) : Target == null ? "" : Target.Blocked ? "Отойдите от створки" :
            Target.Kind == "sliding_entry_door" && Target.TrainMoving ? "Дверь заблокирована: поезд в пути" : InputHints.Action(Verb(Target));
        // "Открыть дверь" reads as an action; the specific door stays visible in the label.
        static string Verb(TrainMechanism m) =>
            m.Label == "Столик" ? (m.IsOpen ? "Сложить столик" : "Разложить столик") :
            m.Label.ToLowerInvariant().Contains("двер") ? (m.IsOpen ? "Закрыть дверь" : "Открыть дверь") + (m.Label.StartsWith("Дверь ") ? " " + m.Label.Substring(6) : "") :
            (m.IsOpen ? "Закрыть: " : "Открыть: ") + m.Label.ToLowerInvariant();
        CharacterController body;
        TrainMechanism[] mechanisms;
        float pitch;
        float verticalSpeed;
        ShiftExperience experience;
        ShiftEnvironment environment;
        FirstPersonHands hands;
        public float ActionLowering { get; private set; }
        bool Performing => (experience != null && experience.Acting) || (hands != null && hands.Active);

        void Start()
        {
            body = GetComponent<CharacterController>();
            mechanisms = FindObjectsByType<TrainMechanism>(FindObjectsSortMode.None);
            experience = FindFirstObjectByType<ShiftExperience>();
            environment = FindFirstObjectByType<ShiftEnvironment>();
            hands = GetComponentInChildren<FirstPersonHands>();
        }

        public void Teleport(Vector3 position)
        {
            var controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalSpeed = 0;
        }

        public void SetMenu(bool open)
        {
            MenuOpen = open;
            TouchMove = TouchLook = Vector2.zero;
            Cursor.lockState = open || Application.isMobilePlatform ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open || Application.isMobilePlatform;
        }

        public void LookAt(Vector3 target)
        {
            Vector3 delta=target-View.transform.position;
            transform.rotation=Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,0);
            pitch=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-80,80);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }

        public void Interact()
        {
            if (MenuOpen || Performing) return;
            if (experience != null && experience.Interact()) return;
            if (Target != null && hands != null) StartCoroutine(hands.Use(Target));
            else Target?.Toggle();
        }

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) SetMenu(!MenuOpen);
            Vector2 move = TouchMove, look = TouchLook;
            TouchLook = Vector2.zero;
            if (!MenuOpen)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                    move += new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
                    look += Mouse.current.delta.ReadValue() * .09f;
                if (!Performing)
                {
                    transform.Rotate(0, look.x, 0);
                    pitch = Mathf.Clamp(pitch - look.y, -80, 80);
                }
                View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                Vector3 velocity = transform.TransformDirection(Vector3.ClampMagnitude(new Vector3(move.x, 0, move.y), 1)) * 2.2f;
                if (Performing) velocity = Vector3.zero;
                verticalSpeed = body.isGrounded && verticalSpeed < 0 ? -2 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
                velocity = KeepInsideWalkArea(velocity, Mathf.Min(Time.deltaTime, .1f));
                velocity.y = verticalSpeed;
                body.Move(velocity * Mathf.Min(Time.deltaTime, .1f));
                if (keyboard?.fKey.wasPressedThisFrame == true) Interact();
            }
            Target = null;
            float score = float.PositiveInfinity;
            var ray = new Ray(View.transform.position, View.transform.forward);
            foreach (var mechanism in mechanisms)
            {
                if (!mechanism.gameObject.activeInHierarchy) continue;
                mechanism.Advance(Time.deltaTime, transform.position, body.radius);
                if (mechanism.Automatic) continue;
                Bounds bounds = mechanism.WorldBounds();
                if (bounds.IntersectRay(ray, out float distance) && distance < mechanism.Range && distance < score)
                {
                    bool occluded = false;
                    foreach (var hit in Physics.RaycastAll(ray, Mathf.Max(0, distance - .03f)))
                        if (hit.collider.GetComponentInParent<TrainMechanism>() != mechanism && hit.collider.GetComponentInParent<FirstPersonController>() == null)
                        { occluded = true; break; }
                    if (!occluded) { Target = mechanism; score = distance; }
                }
            }
        }

        public void SetActionLowering(float value)
        {
            value = Mathf.Clamp(value, 0, .68f);
            View.transform.localPosition += Vector3.down * (value - ActionLowering);
            ActionLowering = value;
        }

        public void TurnForAction(Vector3 target, float blend)
        {
            Vector3 delta = target - View.transform.position;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yaw, 0), Mathf.Clamp01(blend));
            float desiredPitch = Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -65, 65);
            pitch = Mathf.Lerp(pitch, desiredPitch, Mathf.Clamp01(blend));
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public float MoveForAction(Vector3 target, float speed)
        {
            Vector3 delta = target - transform.position; delta.y = 0;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            Vector3 velocity = Vector3.ClampMagnitude(delta / Mathf.Max(dt, .001f), speed);
            body.Move(KeepInsideWalkArea(velocity, dt) * dt);
            delta = target - transform.position; delta.y = 0;
            return delta.magnitude;
        }

        // Closed areas (tracks, beyond fences, roofs) are refused; sliding along the edge keeps walking natural.
        // A player already outside every zone (restore, teleport) is never trapped.
        Vector3 KeepInsideWalkArea(Vector3 velocity, float dt)
        {
            Vector3 feet = transform.position + body.center + Vector3.down * (body.height / 2);
            if (environment != null && environment.Transitioning && WalkArea.InsideTrain(feet) &&
                !WalkArea.InsideTrain(feet + velocity * dt)) return Vector3.zero;
            return dt > 0 ? WalkArea.ConstrainStep(feet, velocity * dt) / dt : velocity;
        }

        void OnApplicationFocus(bool focus) { if (!focus) SetMenu(true); }
    }
}
