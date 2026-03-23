using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives pose data (position + rotation) over UDP,
/// converts it from camera space into Unity space,
/// applies calibration (relative motion),
/// and optionally smooths before applying to a target Transform.
/// </summary>
public class UdpTrackedPoseReceiver : MonoBehaviour
{
    [Header("Networking")]
    [SerializeField] private string listenAddress = "127.0.0.1"; // IP to bind to (localhost for same machine)
    [SerializeField] private int listenPort = 5005;              // UDP port to listen on

    [Header("Target")]
    [SerializeField] private Transform targetObject;             // Object to move/rotate in Unity

    [Header("Calibration")]
    [SerializeField] private bool autoCalibrateOnFirstPacket = true; // Auto-set origin from first received pose

    [Header("Position Smoothing")]
    [SerializeField] private bool enablePositionSmoothing = true;
    [SerializeField] [Min(0.0001f)] private float positionSmoothTime = 0.04f; // Lower = more responsive, less smooth

    [Header("Rotation Smoothing")]
    [SerializeField] private bool enableRotationSmoothing = true;
    [SerializeField] [Min(0.0001f)] private float rotationSmoothSpeed = 18f; // Higher = more responsive

    [Header("Debug")]
    [SerializeField] private bool logPackets = false; // Log incoming JSON packets
    [SerializeField] private bool logCalibration = true;

    /// <summary>
    /// Structure matching incoming JSON packet.
    /// Must be flat for JsonUtility.
    /// </summary>
    [Serializable]
    private class PosePacket
    {
        public int seq;
        public float px, py, pz;
        public float qw, qx, qy, qz;
    }

    /// <summary>
    /// Internal representation after conversion to Unity space.
    /// </summary>
    private struct PoseData
    {
        public int seq;
        public Vector3 position;
        public Quaternion rotation;
        public double receivedTime;
    }

    // Networking
    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning;

    // Shared pose data (thread-safe)
    private readonly object poseLock = new object();
    private bool hasLatestPose;
    private PoseData latestPose;

    // Calibration state
    private bool isCalibrated;
    private Vector3 calibrationPosition;
    private Quaternion calibrationRotation;

    // Smoothing state
    private Vector3 currentSmoothedPosition;
    private Quaternion currentSmoothedRotation;
    private Vector3 positionVelocity;

    private void Start()
    {
        // Ensure we have something to move
        if (targetObject == null)
        {
            Debug.LogError("UdpTrackedPoseReceiver: targetObject is not assigned.");
            enabled = false;
            return;
        }

        // Initialize smoothing state from current transform
        currentSmoothedPosition = targetObject.localPosition;
        currentSmoothedRotation = targetObject.localRotation;

        // Start background UDP listener
        StartReceiver();
    }

    private void Update()
    {
        // Copy latest pose from background thread safely
        PoseData pose;
        bool poseAvailable;

        lock (poseLock)
        {
            pose = latestPose;
            poseAvailable = hasLatestPose;
        }

        if (!poseAvailable)
            return;

        // Handle calibration
        if (!isCalibrated)
        {
            if (autoCalibrateOnFirstPacket)
            {
                CaptureCalibration(pose);
            }
            else
            {
                return;
            }
        }

        // Convert absolute pose into motion relative to calibration origin
        Vector3 relativePosition = pose.position - calibrationPosition;
        Quaternion relativeRotation = Quaternion.Inverse(calibrationRotation) * pose.rotation;

        Vector3 desiredPosition = relativePosition;
        Quaternion desiredRotation = relativeRotation;

        // Smooth position if enabled
        if (enablePositionSmoothing)
        {
            currentSmoothedPosition = Vector3.SmoothDamp(
                currentSmoothedPosition,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime
            );
        }
        else
        {
            currentSmoothedPosition = desiredPosition;
            positionVelocity = Vector3.zero;
        }

        // Smooth rotation if enabled
        if (enableRotationSmoothing)
        {
            float t = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            currentSmoothedRotation = Quaternion.Slerp(currentSmoothedRotation, desiredRotation, t);
        }
        else
        {
            currentSmoothedRotation = desiredRotation;
        }

        // Apply final result to target
        targetObject.localPosition = currentSmoothedPosition;
        targetObject.localRotation = currentSmoothedRotation;
    }

    /// <summary>
    /// Manually trigger calibration using latest received pose.
    /// </summary>
    public void CalibrateNow()
    {
        PoseData pose;
        bool poseAvailable;

        lock (poseLock)
        {
            pose = latestPose;
            poseAvailable = hasLatestPose;
        }

        if (!poseAvailable)
        {
            Debug.LogWarning("Cannot calibrate: no pose received yet.");
            return;
        }

        CaptureCalibration(pose);
    }

    /// <summary>
    /// Clears calibration so next pose defines new origin.
    /// </summary>
    public void ResetCalibration()
    {
        isCalibrated = false;
        positionVelocity = Vector3.zero;

        if (logCalibration)
        {
            Debug.Log("Calibration reset.");
        }
    }

    /// <summary>
    /// Stores current pose as origin (zero position/rotation).
    /// </summary>
    private void CaptureCalibration(PoseData pose)
    {
        calibrationPosition = pose.position;
        calibrationRotation = pose.rotation;
        isCalibrated = true;

        // Reset smoothing and output transform
        currentSmoothedPosition = Vector3.zero;
        currentSmoothedRotation = Quaternion.identity;
        positionVelocity = Vector3.zero;

        targetObject.localPosition = Vector3.zero;
        targetObject.localRotation = Quaternion.identity;

        if (logCalibration)
        {
            Debug.Log($"Calibration captured at seq={pose.seq}");
        }
    }

    /// <summary>
    /// Starts UDP listener on background thread.
    /// </summary>
    private void StartReceiver()
    {
        try
        {
            IPAddress ipAddress = IPAddress.Parse(listenAddress);
            udpClient = new UdpClient(new IPEndPoint(ipAddress, listenPort));

            isRunning = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "UDP Pose Receive Thread"
            };
            receiveThread.Start();

            Debug.Log($"Listening on {listenAddress}:{listenPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start UDP receiver: {ex.Message}");
            enabled = false;
        }
    }

    /// <summary>
    /// Stops UDP thread safely.
    /// </summary>
    private void StopReceiver()
    {
        isRunning = false;

        try { udpClient?.Close(); } catch { }

        try
        {
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(500);
        }
        catch { }

        udpClient = null;
        receiveThread = null;
    }

    /// <summary>
    /// Background loop that receives UDP packets and parses them.
    /// Runs on a separate thread (NOT Unity main thread).
    /// </summary>
    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data);

                if (logPackets)
                {
                    Debug.Log($"RX: {json}");
                }

                // Parse JSON into packet structure
                PosePacket packet = JsonUtility.FromJson<PosePacket>(json);
                if (packet == null)
                    continue;

                // Convert to Unity coordinate system
                PoseData convertedPose = ConvertPacketToUnityPose(packet);

                // Store safely for main thread
                lock (poseLock)
                {
                    latestPose = convertedPose;
                    hasLatestPose = true;
                }
            }
            catch (SocketException)
            {
                if (isRunning)
                    Debug.LogWarning("Socket exception while receiving UDP.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Packet parse error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Converts incoming camera-space pose into Unity-space pose.
    /// </summary>
    private PoseData ConvertPacketToUnityPose(PosePacket packet)
    {
        // Camera coordinate system:
        // +X = right
        // +Y = down
        // +Z = forward

        Vector3 sourcePosition = new Vector3(packet.px, packet.py, packet.pz);
        Quaternion sourceRotation = new Quaternion(packet.qx, packet.qy, packet.qz, packet.qw);

        // Convert position (invert Y)
        Vector3 unityPosition = SourceVectorToUnity(sourcePosition);

        // Convert rotation via basis vectors
        Vector3 sourceRight = sourceRotation * Vector3.right;
        Vector3 sourceDown = sourceRotation * Vector3.up;
        Vector3 sourceForward = sourceRotation * Vector3.forward;

        Vector3 unityRight = SourceVectorToUnity(sourceRight);
        Vector3 unityDown = SourceVectorToUnity(sourceDown);
        Vector3 unityForward = SourceVectorToUnity(sourceForward);

        // Unity up is inverse of "down"
        Vector3 unityUp = -unityDown;

        // Normalize and re-orthogonalize to avoid drift
        unityForward.Normalize();
        unityUp.Normalize();

        unityRight = Vector3.Cross(unityUp, unityForward).normalized;
        unityUp = Vector3.Cross(unityForward, unityRight).normalized;

        Quaternion unityRotation = Quaternion.LookRotation(unityForward, unityUp);

        return new PoseData
        {
            seq = packet.seq,
            position = unityPosition,
            rotation = unityRotation,
            receivedTime = 0
        };
    }

    /// <summary>
    /// Converts a vector from camera space to Unity space.
    /// </summary>
    private Vector3 SourceVectorToUnity(Vector3 sourceVector)
    {
        return new Vector3(
            sourceVector.x,
            -sourceVector.y,
            sourceVector.z
        );
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    private void OnApplicationQuit()
    {
        StopReceiver();
    }
}