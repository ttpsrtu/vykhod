using UnityEngine;

namespace Vykhod
{
    /// <summary>Управление от первого лица: ходьба, бег, присед, прыжок, наклоны, выносливость, вес, травмы, шум.</summary>
    public class FPSController : MonoBehaviour
    {
        public float Stamina = 100, MaxStamina = 100;
        public bool Sprinting, Crouched, Grounded, Moving;
        public float Lean; // -1..1
        public float Pitch;
        CharacterController cc; PlayerNet net; Transform head;
        float yVel, bobT, stepT, standH = 1.8f, crouchH = 1.1f, lastYVel;
        public bool LockLook; // когда открыт инвентарь/меню

        void Awake()
        {
            cc = GetComponent<CharacterController>() ?? gameObject.AddComponent<CharacterController>();
            cc.height = standH; cc.radius = 0.35f; cc.center = new Vector3(0, standH / 2, 0); cc.stepOffset = 0.45f; cc.slopeLimit = 50;
            net = GetComponent<PlayerNet>();
        }

        void OnEnable() { head = transform.Find("Head"); MaxStamina = 100 * Stats.Mul("maxStamina"); Stamina = MaxStamina; }

        public float CarryWeight => 28f * Stats.Mul("carryWeight") + Stats.Get("carryWeightFlat");
        public float Weight => Game.Manager.RaidLoadout?.Weight ?? 0;
        public float Overweight => Mathf.Clamp01((Weight - CarryWeight) / Mathf.Max(1f, CarryWeight));

        float LegFactor()
        {
            var h = net.Health.Value; float f = 1f;
            if (h.lleg <= 0) f *= 0.65f; if (h.rleg <= 0) f *= 0.65f;
            if ((h.Fractured(5) || h.Fractured(6)) && !Stats.Has("health_m30")) f *= 0.8f;
            if (net.InPain && !net.PainFree) f *= 0.92f;
            return f;
        }

        void Update()
        {
            if (Game.State != GameState.Raid || net == null || net.Dead.Value) return;
            bool uiOpen = Game.Manager.UI != null && Game.Manager.UI.IsMenuOpen;
            float dt = Time.deltaTime;
            // --- взгляд
            if (!uiOpen && !LockLook)
            {
                float zoomMul = net.Weapons != null ? net.Weapons.LookSensMul : 1f;
                float sens = Game.Settings.sensitivity * 2.2f * zoomMul;
                float mx = Input.GetAxis("Mouse X") * sens, my = Input.GetAxis("Mouse Y") * sens * (Game.Settings.invertY ? 1 : -1);
                transform.Rotate(0, mx, 0);
                Pitch = Mathf.Clamp(Pitch + my, -85, 85);
            }
            // --- наклоны
            float leanIn = (!uiOpen ? (Input.GetKey(KeyCode.Q) ? -1 : 0) + (Input.GetKey(KeyCode.E) ? 1 : 0) : 0);
            Lean = Mathf.Lerp(Lean, leanIn, dt * 10);
            // --- присед
            if (!uiOpen && (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))) Crouched = !Crouched;
            if (Crouched && Input.GetKey(KeyCode.LeftShift) && Stamina > 10) Crouched = false;
            float targetH = Crouched ? crouchH : standH;
            if (!Crouched && Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, Vector3.up, out var ceil, standH - 0.5f)) { targetH = crouchH; Crouched = true; }
            cc.height = Mathf.Lerp(cc.height, targetH, dt * 12); cc.center = new Vector3(0, cc.height / 2, 0);
            // --- движение
            Vector2 inp = uiOpen ? Vector2.zero : new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (inp.sqrMagnitude > 1) inp.Normalize();
            bool wantSprint = !uiOpen && Input.GetKey(KeyCode.LeftShift) && inp.y > 0.1f && !Crouched && Stamina > 1f && !(net.Weapons != null && net.Weapons.Ads);
            Sprinting = wantSprint;
            bool ads = net.Weapons != null && net.Weapons.Ads;
            float speed = (Sprinting ? 6.4f * Stats.Mul("sprintSpeed") : Crouched ? 1.9f : ads ? 2.3f : 3.8f) * LegFactor();
            float ow = Overweight; if (ow > 0 && !Stats.Has("strength_m50")) speed *= Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(ow * (Stats.Has("strength_m20") ? 0.6f : 1f)));
            if (net.Health.Value.energy <= 0) speed *= 0.8f;
            Vector3 move = (transform.right * inp.x + transform.forward * inp.y) * speed;
            Grounded = cc.isGrounded;
            if (Grounded)
            {
                if (lastYVel < -11f) { float fall = (-lastYVel - 11f) * 4f * Stats.Mul("fallDamage"); if (Stats.Has("vitality_m50")) fall *= 0.5f; net.SelfDamageRpc("legs", fall); }
                yVel = -1f;
                if (!uiOpen && Input.GetKeyDown(KeyCode.Space) && !Crouched && (Stamina > 8 || Stats.Has("endurance_m30")))
                { yVel = 5.2f * Stats.Mul("jump"); if (!Stats.Has("endurance_m30")) Stamina -= 8; Game.Profile.AddSkillXp("endurance", 1); }
            }
            else yVel -= 14f * dt;
            lastYVel = yVel;
            move.y = yVel;
            cc.Move(move * dt);
            Moving = inp.sqrMagnitude > 0.01f && Grounded;
            net.Moving.Value = Moving; net.Crouching.Value = Crouched;
            // --- выносливость
            float drainMul = Mathf.Max(0.2f, 1f + Stats.Get("staminaDrain")) * (1f + ow);
            if (Sprinting && Moving) { Stamina -= 11f * drainMul * dt; Game.Profile.AddSkillXp("endurance", 1.2f * dt); if (ow > 0) Game.Profile.AddSkillXp("strength", 2f * dt); }
            else
            {
                float regen = Moving ? 6f : 9f; if (Crouched) regen *= 1.3f; if (Stats.Has("endurance_m20")) regen *= 1.1f;
                if (Stats.Has("lowHpStamina") && net.Hp < net.MaxHp * 0.4f) regen *= 2f;
                if (net.Health.Value.hydration <= 0) regen *= 0.5f;
                Stamina = Mathf.Min(MaxStamina, Stamina + regen * dt);
            }
            if (Stamina <= 0) { Stamina = 0; Sprinting = false; }
            // --- покачивание камеры, высота головы
            float headY = Crouched ? 1.05f : 1.65f;
            if (Moving) { bobT += dt * (Sprinting ? 12f : Crouched ? 5f : 8f); }
            float bob = Moving ? Mathf.Sin(bobT) * (Sprinting ? 0.05f : 0.025f) : 0;
            if (head != null)
            {
                head.localPosition = Vector3.Lerp(head.localPosition, new Vector3(Lean * 0.45f, headY + bob, 0), dt * 10);
                head.localRotation = Quaternion.Euler(Pitch, 0, -Lean * 14f);
            }
            // --- шаги/шум
            if (Moving)
            {
                stepT += dt * (Sprinting ? 2.6f : Crouched ? 1.1f : 1.8f);
                if (stepT >= 1f)
                {
                    stepT = 0;
                    float radius = (Sprinting ? 24f : Crouched ? 4f : 12f) * Mathf.Max(0.1f, 1f + Stats.Get("noise"));
                    if (Crouched && Stats.Has("covert_m10")) radius = 0;
                    if (radius > 0) { net.StepRpc(transform.position, radius); Fx.Footstep(transform.position, Crouched ? 0.15f : Sprinting ? 0.6f : 0.35f); }
                    Game.Profile.AddSkillXp("covert", Crouched ? 0.6f : 0.1f);
                }
            }
        }
    }
}
