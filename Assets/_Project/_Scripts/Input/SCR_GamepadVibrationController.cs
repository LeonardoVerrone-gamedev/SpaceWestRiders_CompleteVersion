using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(SCR_RayBasedCarPhysics))]
[RequireComponent(typeof(SCR_CarInput))]
public class SCR_GamepadVibrationController : MonoBehaviour
{
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;
    [SerializeField] private SCR_CarInput carInput;

    private Gamepad gamepad;
    private Coroutine vibrationRoutine;

    [Header("Vibration Multipliers")]
    [SerializeField] float collisionMultiplier = 0.8f;
    [SerializeField] float hardAccelMultiplier = 0.4f;
    [SerializeField] float hardBrakeMultiplier = 0.5f;
    [SerializeField] float driftMultiplier = 0.3f;
    [SerializeField] float jumpMultiplier = 0.6f;
    [SerializeField] float turboMultiplier = 0.9f;

    void OnEnable()
    {
        carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        carInput   = GetComponent<SCR_CarInput>();
    }

    void Start()
    {
        SubscribeEvents();
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
        StopVibration();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
        StopVibration();
    }

    void SubscribeEvents()
    {
        carPhysics.OnCollision       += HandleCollision;
       carPhysics.OnHardAcceleration+= HandleHardAccel;
        carPhysics.OnHardBrake       += HandleHardBrake;
        //carPhysics.OnDriftStart      += HandleDrift;
        carPhysics.OnJump            += HandleJump;
        carPhysics.OnLand            += HandleLand;
        carPhysics.OnTurboStart      += HandleTurboStart;
        carPhysics.OnTurboEnd        += HandleTurboEnd;
    }

    void UnsubscribeEvents()
    {
        carPhysics.OnCollision       -= HandleCollision;
        carPhysics.OnHardAcceleration-= HandleHardAccel;
        carPhysics.OnHardBrake       -= HandleHardBrake;
        //carPhysics.OnDriftStart      -= HandleDrift;
        carPhysics.OnJump            -= HandleJump;
        carPhysics.OnLand            -= HandleLand;
        carPhysics.OnTurboStart      -= HandleTurboStart;
        carPhysics.OnTurboEnd        -= HandleTurboEnd;
    }

    Gamepad GetGamepad()
    {
        if (gamepad != null) return gamepad;

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            foreach (var device in playerInput.devices)
            {
                if (device is Gamepad pad)
                {
                    gamepad = pad;
                    return pad;
                }
            }
        }

        return null;
    }

    void Vibrate(float lowFreq, float highFreq, float duration)
    {
        Gamepad pad = GetGamepad();
        if (pad == null)
        {
            return;
        }

        if (vibrationRoutine != null)
            StopCoroutine(vibrationRoutine);

        vibrationRoutine = StartCoroutine(VibrationCoroutine(pad, lowFreq, highFreq, duration));
    }

    IEnumerator VibrationCoroutine(Gamepad pad, float low, float high, float duration)
    {
        pad.SetMotorSpeeds(low, high);
        yield return new WaitForSeconds(duration);
        pad.SetMotorSpeeds(0f, 0f);
    }

    void StopVibration()
    {
        Gamepad pad = GetGamepad();
        if (pad != null)
            pad.SetMotorSpeeds(0f, 0f);
    }

    // ================= EVENT HANDLERS =================

    void HandleCollision(float force, Vector3 dir)
    {
        float intensity = Mathf.Clamp01(force / 30f) * collisionMultiplier;
        Vibrate(intensity, intensity * 0.5f, 0.4f);
    }

    void HandleHardAccel(float intensity)
    {
        float rumble = intensity * hardAccelMultiplier;
        Vibrate(rumble, rumble * 0.3f, 0.2f);
    }

    void HandleHardBrake(float intensity)
    {
        float rumble = intensity * hardBrakeMultiplier;
        Vibrate(rumble * 0.5f, rumble, 0.25f);
    }

    void HandleJump(float airTime)
    {
        float intensity = Mathf.Clamp01(airTime / 2f) * jumpMultiplier;
        Vibrate(intensity, intensity, 0.3f);
    }

    void HandleLand(float airTime)
    {
        float intensity = Mathf.Clamp01(airTime / 2f);
        Vibrate(intensity, intensity * 0.4f, 0.35f);
    }

    void HandleTurboStart()
    {
        Vibrate(turboMultiplier, turboMultiplier * 0.5f, 0.5f);
    }

    void HandleTurboEnd()
    {
        StopVibration();
    }
}