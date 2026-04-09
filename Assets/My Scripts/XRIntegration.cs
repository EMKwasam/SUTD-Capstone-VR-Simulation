using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives position data over UDP,
/// converts it from camera space into Unity space,
/// applies calibration (relative motion),
/// and optionally smooths before applying to a target Transform.
/// </summary>
public class UdpTrackedPoseReceiver : MonoBehaviour
{
    /// <summary>
    /// Enum for selecting position smoothing algorithm.
    /// </summary>
    public enum PositionSmoothingMethod
    {
        EMA = 1,     // Exponential Moving Average
        OneEuro = 2  // Adaptive One Euro filter
    }

    [Header("Networking")]
    [SerializeField] private string listenAddress = "127.0.0.1"; // IP to bind to (localhost for same machine)
    [SerializeField] private int listenPort = 5005;              // UDP port to listen on

    [Header("Target")]
    [SerializeField] private Transform targetObject;             // Object to move in Unity

    [Header("Locking")]
    [SerializeField] private JoystickToolMovement joystickToolMovement; // Reads the shared movement lock state

    [Header("Calibration")]
    [SerializeField] private bool autoCalibrateOnFirstPacket = true; // Auto-set origin from first received pose

    [Header("Position Smoothing")]
    [SerializeField] private bool enablePositionSmoothing = true;
    [SerializeField] private PositionSmoothingMethod positionSmoothingMethod = PositionSmoothingMethod.OneEuro;
    [SerializeField] [Range(0f, 1f)] private float positionEmaAlpha = 0.2f; // Higher alpha = more responsive to new values

    [Header("One Euro Position Smoothing (30 Hz Defaults)")]
    [SerializeField] [Min(0.01f)] private float oneEuroMinCutoff = 1.1f;
    [SerializeField] [Min(0f)] private float oneEuroBeta = 0.05f;
    [SerializeField] [Min(0.01f)] private float oneEuroDerivativeCutoff = 1.0f;

    [Header("Position Prefilter")]
    [SerializeField] private bool enableMedianPositionPrefilter = false;

    [Header("Position Scaling")]
    [SerializeField] private bool enablePositionScaling = false;
    [SerializeField] private float positionScaleMultiplier = 1f;

    [Header("Deadband")]
    [SerializeField] private bool enableDeadband = false;
    [SerializeField] [Min(0f)] private float positionDeadbandMeters = 0.003f;

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
    }

    /// <summary>
    /// Internal representation after conversion to Unity space.
    /// </summary>
    private struct PoseData
    {
        public int seq;
        public Vector3 position;
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

    // Smoothing state
    private Vector3 currentSmoothedPosition;

    // Jitter filtering state
    private readonly Vector3[] medianPositionBuffer = new Vector3[3];
    private int medianPositionCount;
    private int medianPositionWriteIndex;
    private bool hasDeadbandReference;
    private Vector3 deadbandReferencePosition;

    // One Euro state
    private bool oneEuroInitialized;
    private Vector3 oneEuroPreviousRawPosition;
    private Vector3 oneEuroFilteredPosition;
    private Vector3 oneEuroFilteredDerivative;
    private bool isExternallyMovementLocked;

    public bool IsExternallyMovementLocked => isExternallyMovementLocked;

    private void Start()
    {
        // Ensure we have something to move
        if (targetObject == null)
        {
            Debug.LogError("UdpTrackedPoseReceiver: targetObject is not assigned.");
            enabled = false;
            return;
        }

        if (joystickToolMovement == null)
        {
            joystickToolMovement = GetComponent<JoystickToolMovement>();

            if (joystickToolMovement == null)
            {
                joystickToolMovement = targetObject.GetComponentInParent<JoystickToolMovement>();
            }
        }

        // Initialize smoothing state from current transform
        currentSmoothedPosition = targetObject.localPosition;

        // Start background UDP listener
        StartReceiver();
    }

    private void OnValidate()
    {
        if (!Enum.IsDefined(typeof(PositionSmoothingMethod), positionSmoothingMethod))
        {
            positionSmoothingMethod = PositionSmoothingMethod.OneEuro;
        }
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
        Vector3 desiredPosition = enablePositionScaling
            ? relativePosition * positionScaleMultiplier
            : relativePosition;

        if (enableMedianPositionPrefilter)
        {
            desiredPosition = ApplyMedianPositionPrefilter(desiredPosition);
        }

        if (enableDeadband)
        {
            desiredPosition = ApplyDeadband(desiredPosition);
        }
        else
        {
            deadbandReferencePosition = desiredPosition;
            hasDeadbandReference = true;
        }

        if (IsMovementLocked())
        {
            return;
        }

        // Smooth position if enabled
        if (enablePositionSmoothing)
        {
            if (positionSmoothingMethod == PositionSmoothingMethod.EMA)
            {
                currentSmoothedPosition = Vector3.Lerp(
                    currentSmoothedPosition,
                    desiredPosition,
                    positionEmaAlpha
                );
            }
            else if (positionSmoothingMethod == PositionSmoothingMethod.OneEuro)
            {
                currentSmoothedPosition = ApplyOneEuroPositionFilter(desiredPosition, Time.deltaTime);
            }
        }
        else
        {
            currentSmoothedPosition = desiredPosition;
        }

        // Apply final result to target
        targetObject.localPosition = currentSmoothedPosition;
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
        ResetJitterFilters();

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
        isCalibrated = true;

        // Reset smoothing and output transform
        currentSmoothedPosition = Vector3.zero;
        ResetJitterFilters();

        targetObject.localPosition = Vector3.zero;

        if (logCalibration)
        {
            Debug.Log($"Calibration captured at seq={pose.seq}");
        }
    }

    private Vector3 ApplyMedianPositionPrefilter(Vector3 sample)
    {
        medianPositionBuffer[medianPositionWriteIndex] = sample;
        medianPositionWriteIndex = (medianPositionWriteIndex + 1) % medianPositionBuffer.Length;

        if (medianPositionCount < medianPositionBuffer.Length)
        {
            medianPositionCount++;
            return sample;
        }

        Vector3 a = medianPositionBuffer[0];
        Vector3 b = medianPositionBuffer[1];
        Vector3 c = medianPositionBuffer[2];

        return new Vector3(
            MedianOfThree(a.x, b.x, c.x),
            MedianOfThree(a.y, b.y, c.y),
            MedianOfThree(a.z, b.z, c.z)
        );
    }

    private static float MedianOfThree(float a, float b, float c)
    {
        return a + b + c - Mathf.Min(a, Mathf.Min(b, c)) - Mathf.Max(a, Mathf.Max(b, c));
    }

    private Vector3 ApplyDeadband(Vector3 position)
    {
        if (!hasDeadbandReference)
        {
            deadbandReferencePosition = position;
            hasDeadbandReference = true;
            return position;
        }

        float positionThresholdSqr = positionDeadbandMeters * positionDeadbandMeters;
        if ((position - deadbandReferencePosition).sqrMagnitude < positionThresholdSqr)
        {
            position = deadbandReferencePosition;
        }
        else
        {
            deadbandReferencePosition = position;
        }

        return position;
    }

    private void ResetJitterFilters()
    {
        medianPositionCount = 0;
        medianPositionWriteIndex = 0;
        hasDeadbandReference = false;
        deadbandReferencePosition = Vector3.zero;
        oneEuroInitialized = false;
        oneEuroPreviousRawPosition = Vector3.zero;
        oneEuroFilteredPosition = Vector3.zero;
        oneEuroFilteredDerivative = Vector3.zero;
    }

    private Vector3 ApplyOneEuroPositionFilter(Vector3 rawPosition, float deltaTime)
    {
        float dt = Mathf.Max(0.0001f, deltaTime);

        if (!oneEuroInitialized)
        {
            oneEuroInitialized = true;
            oneEuroPreviousRawPosition = rawPosition;
            oneEuroFilteredPosition = rawPosition;
            oneEuroFilteredDerivative = Vector3.zero;
            return rawPosition;
        }

        Vector3 rawDerivative = (rawPosition - oneEuroPreviousRawPosition) / dt;
        float derivativeAlpha = ComputeOneEuroAlpha(oneEuroDerivativeCutoff, dt);
        oneEuroFilteredDerivative = Vector3.Lerp(oneEuroFilteredDerivative, rawDerivative, derivativeAlpha);

        float adaptiveCutoff = oneEuroMinCutoff + oneEuroBeta * oneEuroFilteredDerivative.magnitude;
        float positionAlpha = ComputeOneEuroAlpha(adaptiveCutoff, dt);
        oneEuroFilteredPosition = Vector3.Lerp(oneEuroFilteredPosition, rawPosition, positionAlpha);

        oneEuroPreviousRawPosition = rawPosition;
        return oneEuroFilteredPosition;
    }

    private static float ComputeOneEuroAlpha(float cutoff, float deltaTime)
    {
        float safeCutoff = Mathf.Max(0.0001f, cutoff);
        float tau = 1f / (2f * Mathf.PI * safeCutoff);
        return 1f / (1f + tau / deltaTime);
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
    /// Converts incoming camera-space position into Unity-space position.
    /// </summary>
    private PoseData ConvertPacketToUnityPose(PosePacket packet)
    {
        // Camera coordinate system:
        // +X = right
        // +Y = down
        // +Z = forward

        Vector3 sourcePosition = new Vector3(packet.px, packet.py, packet.pz);

        // Convert position using configured camera-to-Unity axis mapping
        Vector3 unityPosition = SourceVectorToUnity(sourcePosition);

        return new PoseData
        {
            seq = packet.seq,
            position = unityPosition
        };
    }

    /// <summary>
    /// Converts a vector from camera space to Unity space.
    /// </summary>
    private Vector3 SourceVectorToUnity(Vector3 sourceVector)
    {
        return new Vector3(
            sourceVector.z,
            -sourceVector.x,
            sourceVector.y
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

    private bool IsMovementLocked()
    {
        if (isExternallyMovementLocked)
        {
            return true;
        }

        return joystickToolMovement != null && joystickToolMovement.IsMovementLocked;
    }

    public void SetMovementLocked(bool locked)
    {
        isExternallyMovementLocked = locked;
    }
}