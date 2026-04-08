using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Unity-side UDP test sender for the UdpTrackedPoseReceiver.
/// 
/// This script simulates a tracked controller by sending position and rotation
/// packets over UDP in the same JSON format as the real depth camera will use.
/// 
/// Default controls:
/// - W/S = forward/back
/// - A/D = left/right
/// - Q/E = down/up
/// - Arrow keys = pitch/yaw
/// - Z/C = roll
/// - Space = reset pose
/// </summary>
public class UdpTrackedPoseTestSender : MonoBehaviour
{
    [Header("Networking")]
    [SerializeField] private string targetIp = "127.0.0.1";
    [SerializeField] private int targetPort = 5005;

    [Header("Send Rate")]
    [SerializeField] [Min(1f)] private float sendRateHz = 30f;

    [Header("Manual Motion")]
    [SerializeField] private float moveSpeed = 0.75f;     // metres per second
    [SerializeField] private float rotateSpeed = 90f;     // degrees per second

    [Header("Auto Motion")]
    [SerializeField] private bool useAutoMotion = false;
    [SerializeField] private float autoMotionRadius = 0.20f;
    [SerializeField] private float autoMotionHeight = 0.10f;
    [SerializeField] private float autoMotionSpeed = 1.0f;
    [SerializeField] private float autoYawSpeed = 45f;

    [Header("Starting Pose In Camera Space")]
    [SerializeField] private Vector3 startPosition = new Vector3(0f, 0f, 0.5f);

    [Header("Debug")]
    [SerializeField] private bool logPackets = false;

    [Serializable]
    private class PosePacket
    {
        public int seq;
        public float px;
        public float py;
        public float pz;
        public float qw;
        public float qx;
        public float qy;
        public float qz;
    }

    private UdpClient udpClient;
    private float sendInterval;
    private float sendTimer;
    private int sequenceNumber;

    // This pose is stored in the CAMERA coordinate system:
    // +X = right
    // +Y = down
    // +Z = forward
    private Vector3 currentPosition;
    private Quaternion currentRotation;

    private void Start()
    {
        udpClient = new UdpClient();
        sendInterval = 1f / sendRateHz;

        currentPosition = startPosition;
        currentRotation = Quaternion.identity;
    }

    private void Update()
    {
        if (useAutoMotion)
        {
            UpdateAutoMotion();
        }
        else
        {
            UpdateManualMotion();
        }

        sendTimer += Time.deltaTime;

        while (sendTimer >= sendInterval)
        {
            sendTimer -= sendInterval;
            SendCurrentPose();
        }
    }

    /// <summary>
/// Updates the simulated pose using keyboard controls.
/// Motion is applied in camera space.
/// Uses the Unity Input System package.
/// </summary>
private void UpdateManualMotion()
{
    float dt = Time.deltaTime;

    Keyboard keyboard = Keyboard.current;
    if (keyboard == null)
        return;

    Vector3 translation = Vector3.zero;

    // Camera-space translation
    if (keyboard.aKey.isPressed) translation.x -= moveSpeed * dt;
    if (keyboard.dKey.isPressed) translation.x += moveSpeed * dt;
    if (keyboard.qKey.isPressed) translation.y += moveSpeed * dt; // +Y is down in camera space
    if (keyboard.eKey.isPressed) translation.y -= moveSpeed * dt; // negative Y = up
    if (keyboard.wKey.isPressed) translation.z += moveSpeed * dt;
    if (keyboard.sKey.isPressed) translation.z -= moveSpeed * dt;

    currentPosition += translation;

    float pitch = 0f;
    float yaw = 0f;
    float roll = 0f;

    // Rotation controls
    if (keyboard.upArrowKey.isPressed) pitch -= rotateSpeed * dt;
    if (keyboard.downArrowKey.isPressed) pitch += rotateSpeed * dt;
    if (keyboard.leftArrowKey.isPressed) yaw -= rotateSpeed * dt;
    if (keyboard.rightArrowKey.isPressed) yaw += rotateSpeed * dt;
    if (keyboard.zKey.isPressed) roll -= rotateSpeed * dt;
    if (keyboard.cKey.isPressed) roll += rotateSpeed * dt;

    Quaternion deltaRotation = Quaternion.Euler(pitch, yaw, roll);
    currentRotation = deltaRotation * currentRotation;
    currentRotation.Normalize();

    if (keyboard.spaceKey.wasPressedThisFrame)
    {
        ResetPose();
    }
}

    /// <summary>
    /// Updates the simulated pose with a simple looping motion.
    /// Useful when you want to test the receiver hands-free.
    /// </summary>
    private void UpdateAutoMotion()
    {
        float t = Time.time * autoMotionSpeed;

        currentPosition = startPosition + new Vector3(
            Mathf.Cos(t) * autoMotionRadius,
            Mathf.Sin(t * 0.7f) * autoMotionHeight,
            Mathf.Sin(t) * autoMotionRadius
        );

        currentRotation = Quaternion.Euler(
            Mathf.Sin(t * 0.8f) * 20f,
            t * autoYawSpeed,
            Mathf.Cos(t * 0.6f) * 15f
        );
    }

    /// <summary>
    /// Sends the current simulated camera-space pose as a UDP JSON packet.
    /// </summary>
    private void SendCurrentPose()
    {
        PosePacket packet = new PosePacket
        {
            seq = sequenceNumber++,
            px = currentPosition.x,
            py = currentPosition.y,
            pz = currentPosition.z,
            qw = currentRotation.w,
            qx = currentRotation.x,
            qy = currentRotation.y,
            qz = currentRotation.z
        };

        string json = JsonUtility.ToJson(packet);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            udpClient.Send(bytes, bytes.Length, targetIp, targetPort);

            if (logPackets)
            {
                Debug.Log($"UDP Test Sender TX: {json}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"UdpTrackedPoseTestSender: Failed to send packet. {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the simulated pose back to the configured starting pose.
    /// </summary>
    public void ResetPose()
    {
        currentPosition = startPosition;
        currentRotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        udpClient?.Close();
        udpClient = null;
    }

    private void OnApplicationQuit()
    {
        udpClient?.Close();
        udpClient = null;
    }
}