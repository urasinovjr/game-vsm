using System.Collections;
using System.Linq;
using UnityEngine;

namespace GameVSM
{
    // Owns the presentation of a conductor's physical action, never its server outcome.
    public sealed class FirstPersonHands : MonoBehaviour
    {
        public Transform LeftArm, LeftElbow, LeftHand, RightArm, RightElbow, RightHand;
        public bool Active { get; private set; }
        public bool Succeeded { get; private set; }
        public float Progress { get; private set; }
        public float ContactError { get; private set; }
        FirstPersonController player;
        Renderer[] surfaces;
        Transform[] bones;
        Quaternion[] rest;
        Vector3 leftPalm, rightPalm;
        Vector3 leftContact, rightContact;
        float grip, blend;
        string action;
        Transform radio, clipboard;
        Transform luggage;
        TrainMechanism mechanism;
        LuggageVisual bag;
        static readonly Vector3 Rack = new(46.96f, 2.625f, 1.10f);
        static readonly Quaternion RackRotation = Quaternion.Euler(0, 0, 90);

        void Start()
        {
            player = GetComponentInParent<FirstPersonController>();
            bones = GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Arm") || t.name.Contains("Hand")).ToArray();
            rest = bones.Select(b => b.localRotation).ToArray();
            surfaces = GetComponentsInChildren<Renderer>();
            leftPalm = PalmOffset(LeftHand); rightPalm = PalmOffset(RightHand);
            radio = ActionProps.CreateRadio(transform); radio.gameObject.SetActive(false);
            clipboard = ActionProps.CreateClipboard(transform); clipboard.gameObject.SetActive(false);
        }

        static Vector3 PalmOffset(Transform hand)
        {
            var finger = hand.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("HandMiddle1"));
            return Quaternion.Inverse(hand.rotation) * ((finger != null ? finger.position : hand.position) - hand.position);
        }

        public IEnumerator Use(TrainMechanism target)
        {
            if (Active || target.Automatic || (target.Kind == "sliding_entry_door" && target.TrainMoving)) yield break;
            mechanism = target; action = "mechanism"; Active = true; Succeeded = true; Progress = blend = grip = 0;
            rightContact = target.WorldBounds().ClosestPoint(player.transform.position + Vector3.up * 1.15f);
            Vector3 direction = player.transform.position - rightContact; direction.y = 0;
            var approach = rightContact + direction.normalized * .43f; approach.y = player.transform.position.y;
            float lower = Mathf.Clamp(player.View.transform.position.y - rightContact.y - .18f, 0, .68f);
            yield return Approach(approach, rightContact, lower, 0, .2f);
            if (Succeeded)
            {
                for (float t = 0; t < .5f; t += Time.deltaTime)
                { blend = Smooth(t / .5f); Progress = .2f + .3f * blend; yield return null; }
                target.Toggle();
                for (float t = 0; t < .7f; t += Time.deltaTime)
                { blend = 1 - Smooth(t / .7f); Progress = .5f + .5f * t / .7f; player.SetActionLowering(lower * blend); yield return null; }
            }
            Active = false; mechanism = null; blend = grip = 0; player.SetActionLowering(0);
        }

        public IEnumerator Present(string node, Transform target)
        {
            action = node; luggage = target; bag = target != null ? target.GetComponent<LuggageVisual>() : null;
            Active = true; Succeeded = true; Progress = 0; blend = 0; grip = 0;
            if (node == "bag_action" && bag != null)
                yield return PlaceBag();
            else if (node is "equipment" or "handover" or "inspect" or "inspection_fix" or "final_check")
            {
                float duration = node == "equipment" ? 2.2f : node == "handover" ? 2.4f : 1.6f;
                for (float t = 0; t < duration; t += Time.deltaTime)
                {
                    Progress = t / duration;
                    blend = Smooth(Progress / .22f) * Smooth((1 - Progress) / .22f);
                    grip = blend;
                    yield return null;
                }
            }
            else
                yield return new WaitForSeconds(.5f);
            Progress = 1; blend = grip = 0; Active = false;
            player.SetActionLowering(0);
        }

        IEnumerator PlaceBag()
        {
            var direction = player.transform.position - luggage.position; direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = -player.transform.forward;
            var approach = luggage.position + direction.normalized * .50f; approach.y = player.transform.position.y;
            yield return Approach(approach, luggage.position + Vector3.up * .45f, .63f, 0, .12f);
            if (!Succeeded) yield break;
            for (float t = 0; t < .7f; t += Time.deltaTime)
            {
                Progress = .12f + .10f * t / .7f; blend = grip = Smooth(t / .7f);
                BagContacts(); yield return null;
            }
            Vector3 start = luggage.position; Quaternion rotation = luggage.rotation;
            for (float t = 0; t < 1; t += Time.deltaTime)
            {
                float u = Smooth(t); Progress = .22f + .18f * u;
                player.SetActionLowering(.63f * (1 - u));
                var carry = CarryPosition();
                luggage.position = Vector3.Lerp(start, carry, u);
                luggage.rotation = Quaternion.Slerp(rotation, player.transform.rotation, u);
                BagContacts(); yield return null;
            }
            // Move the conductor along the vestibule with CharacterController collision, never teleport the bag across it.
            // The shelf face blocks the capsule before its centre reaches the shelf.
            // Stop on the aisle side, then let the arms place the case across the lip.
            var rackApproach = Rack + new Vector3(-.50f, 0, -.70f); rackApproach.y = player.transform.position.y;
            yield return Approach(rackApproach, Rack, .08f, .40f, .63f, true);
            if (!Succeeded) yield break;
            start = luggage.position; rotation = luggage.rotation;
            Vector3 above = Rack + new Vector3(0, .17f, -.32f);
            for (float t = 0; t < 1.25f; t += Time.deltaTime)
            {
                float u = Smooth(t / 1.25f); Progress = .63f + .19f * u;
                luggage.position = Vector3.Lerp(start, above, u);
                luggage.rotation = Quaternion.Slerp(rotation, RackRotation, u);
                BagContacts(); yield return null;
            }
            for (float t = 0; t < .85f; t += Time.deltaTime)
            {
                float u = Smooth(t / .85f); Progress = .82f + .10f * u;
                blend = grip = 1 - u;
                luggage.position = Vector3.Lerp(above, Rack, u); luggage.rotation = RackRotation;
                BagContacts(); yield return null;
            }
            luggage.SetPositionAndRotation(Rack, RackRotation);
            for (float t = 0; t < .55f; t += Time.deltaTime)
            {
                float u = Smooth(t / .55f); Progress = .92f + .08f * u; blend = grip = 0;
                player.SetActionLowering(.08f * (1 - u)); BagContacts(); yield return null;
            }
        }

        IEnumerator Approach(Vector3 position, Vector3 look, float lowering, float from, float to, bool carrying = false)
        {
            float initial = Vector3.Distance(player.transform.position, position);
            float initialLowering = player.ActionLowering;
            for (float t = 0; t < 5; t += Time.deltaTime)
            {
                float remaining = player.MoveForAction(position, .72f);
                float u = initial < .04f ? 1 : 1 - Mathf.Clamp01(remaining / initial);
                Progress = Mathf.Lerp(from, to, u);
                player.SetActionLowering(Mathf.Lerp(initialLowering, lowering, Smooth(Mathf.Max(u, t / .8f))));
                player.TurnForAction(look, Time.deltaTime * 4);
                if (carrying)
                {
                    luggage.position = CarryPosition();
                    luggage.rotation = player.transform.rotation; BagContacts();
                }
                yield return null;
                if (remaining < .045f && t > .55f) yield break;
            }
            Succeeded = false;
        }

        void BagContacts()
        {
            // Move both palms from the carrying grip onto the near shell as the case turns.
            // Keeping the right hand on the far handle would pull its arm across the camera.
            float placing = Smooth((Progress - .58f) / .15f);
            rightContact = Vector3.Lerp(bag.Grip.position,
                luggage.TransformPoint(new Vector3(.13f, .18f, -.08f)), placing);
            leftContact = Vector3.Lerp(luggage.TransformPoint(new Vector3(-.10f, .40f, -.04f)),
                luggage.TransformPoint(new Vector3(-.12f, .25f, -.08f)), placing);
        }

        Vector3 CarryPosition() => player.transform.position + player.transform.forward * .20f + Vector3.up * .72f;

        void LateUpdate()
        {
            if (player == null) return;
            bool visible = Active && (action == "mechanism" || action == "bag_action" || action == "equipment" || action == "handover" || action == "inspect" || action == "inspection_fix" || action == "final_check");
            foreach (var r in surfaces) r.enabled = visible;
            radio.gameObject.SetActive(visible && action == "equipment");
            clipboard.gameObject.SetActive(visible && action != "equipment" && action != "bag_action" && action != "mechanism");
            if (!visible) return;
            transform.SetPositionAndRotation(player.View.transform.position - Vector3.up * 1.55f - player.transform.forward * .12f, player.transform.rotation);
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = rest[i];
            Vector3 forward = player.transform.forward, right = player.transform.right, camera = player.View.transform.position;
            if (action == "equipment")
            {
                rightContact = camera + forward * .34f + right * .16f - Vector3.up * .30f;
                leftContact = rightContact - right * .07f + Vector3.up * (.04f + .012f * Mathf.Sin(Progress * Mathf.PI * 4));
                radio.SetPositionAndRotation(rightContact + Vector3.up * .025f, player.transform.rotation);
            }
            else if (action == "mechanism")
            {
                if (mechanism != null) rightContact = mechanism.WorldBounds().ClosestPoint(rightContact);
                leftContact = camera + forward * .18f - right * .24f - Vector3.up * .65f;
            }
            else if (action != "bag_action")
            {
                leftContact = camera + forward * .34f - right * .14f - Vector3.up * .42f;
                rightContact = leftContact + right * (.14f + .025f * Mathf.Sin(Progress * Mathf.PI * 8)) + Vector3.up * .035f;
                clipboard.SetPositionAndRotation(leftContact + right * .075f, player.transform.rotation * Quaternion.Euler(58, 0, 0));
            }
            var leftRest = LeftHand.position; var rightRest = RightHand.position;
            Vector3 palmDirection = action == "bag_action" && luggage != null ? -luggage.up : forward;
            ContactError = Mathf.Max(Solve(LeftArm, LeftElbow, LeftHand, Vector3.Lerp(leftRest, leftContact, blend), leftPalm, palmDirection, -right),
                Solve(RightArm, RightElbow, RightHand, Vector3.Lerp(rightRest, rightContact, blend), rightPalm, palmDirection, right));
            Vector3 curlAxis = action == "bag_action" && luggage != null ? luggage.right : right;
            Curl(LeftHand, curlAxis); Curl(RightHand, curlAxis);
        }

        float Solve(Transform arm, Transform elbow, Transform hand, Vector3 contact, Vector3 palmOffset, Vector3 direction, Vector3 outward)
        {
            Quaternion handRotation = Quaternion.FromToRotation(hand.rotation * palmOffset, direction) * hand.rotation;
            Vector3 wrist = contact - handRotation * palmOffset;
            float upper = Vector3.Distance(arm.position, elbow.position), lower = Vector3.Distance(elbow.position, hand.position);
            Vector3 delta = wrist - arm.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .005f);
            Vector3 axis = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(outward * .65f - Vector3.up, axis).normalized;
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            Vector3 elbowPoint = arm.position + axis * along + pole * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            arm.rotation = Quaternion.FromToRotation(elbow.position - arm.position, elbowPoint - arm.position) * arm.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, wrist - elbow.position) * elbow.rotation;
            hand.rotation = handRotation;
            return Vector3.Distance(hand.position + hand.rotation * palmOffset, contact);
        }

        void Curl(Transform hand, Vector3 axis)
        {
            foreach (var joint in hand.GetComponentsInChildren<Transform>())
                if (joint != hand && joint.childCount > 0 && !joint.name.Contains("Thumb"))
                    joint.rotation = Quaternion.AngleAxis(18 * grip, axis) * joint.rotation;
        }
        static float Smooth(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(value));
    }
}
