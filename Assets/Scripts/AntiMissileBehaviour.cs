using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

public class AntiMissileBehaviour : Jundroo.SimplePlanes.ModTools.Parts.PartModifierBehaviour
{
    private AntiMissile modifier;

    // Static Update runs once per frame, regardless of the number of class instances
    // Targets are updated at the selected interval
    public static int LastStaticUpdateFrame = -1;
    public static float TargetUpdateInterval = 0.25f;
    public static float TargetUpdateTimer = 0.25f;

    public static List<AntiMissileTarget> Targets = new List<AntiMissileTarget>(16);

    public static string[] SupportedTargetTypes =
    {
        "AntiAircraftMissileScript",
        "MissileScript",
        "RocketScript",
        "BombScript"
    };

    public GameObject ParentAircraftObject;
    private Transform BallTransform;
    private LineRenderer LaserLine;

    public AntiMissileTarget LockedTarget = null;
    private float BallPitchAngle = 0f;
    private float BallYawAngle = 0f;
    private float TimeToTargetDetonation;
    private float TimeToReload;

    private float ScanInterval;
    private float TimeToNextScan;

    private int RemainingAmmo;
    private bool InfiniteAmmo = false;

    // For smart allocation mode only
    private List<AntiMissileBehaviour> OtherAmballsOnAircraft = new List<AntiMissileBehaviour>();

    private void Start()
    {
        modifier = (AntiMissile)PartModifier;

        Component[] parentComponents = GetComponentsInParent<Component>();
        foreach (Component c in parentComponents)
        {
            if (c.GetType().Name == "AircraftScript")
            {
                ParentAircraftObject = c.gameObject;
                break;
            }
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>();
        foreach (Transform t in childTransforms)
        {
            if (t.gameObject.name == "AntiMissileBall")
            {
                BallTransform = t;
                break;
            }
        }

        LaserLine = GetComponentInChildren<LineRenderer>();
        LaserLine.startColor = modifier.LaserColor;
        LaserLine.endColor = modifier.LaserColor;
        LaserLine.startWidth = modifier.LaserWidth;
        LaserLine.endWidth = modifier.LaserWidth;
        LaserLine.enabled = false;

        TimeToTargetDetonation = modifier.DetonateTime;
        TimeToReload = 0f;

        ScanInterval = Mathf.Max(0.02f, modifier.ReloadTime * 0.2f);
        TimeToNextScan = ScanInterval;

        RemainingAmmo = modifier.MaxAmmo;
        if (modifier.MaxAmmo <= 0)
        {
            InfiniteAmmo = true;
        }

        AntiMissileBehaviour[] amballsAll = FindObjectsOfType<AntiMissileBehaviour>();
        foreach (AntiMissileBehaviour a in amballsAll)
        {
            if (a.ParentAircraftObject == this.ParentAircraftObject && a != this)
            {
                OtherAmballsOnAircraft.Add(a);
            }
        }
    }

    private void Update()
    {
        // Skip the update if not in the level
        if (!ServiceProvider.Instance.GameState.IsInLevel)
        {
            return;
        }
        else if (ServiceProvider.Instance.GameState.IsPaused)
        {
            return;
        }

        // BEGIN Static Update
        if (Time.frameCount != LastStaticUpdateFrame)
        {
            LastStaticUpdateFrame = Time.frameCount;

            // Remove targets that have been deleted
            foreach (AntiMissileTarget t in Targets.ToArray())
            {
                if (t.Script == null)
                {
                    Targets.Remove(t);
                    Debug.Log("Removed AntiMissileTarget as it no longer exists");
                }
            }

            TargetUpdateTimer -= Time.deltaTime;
            if (TargetUpdateTimer <= 0f)
            {
                TargetUpdateTimer = TargetUpdateInterval;

                // Find and add targets to the list
                Component[] allComponents = FindObjectsOfType<Component>();
                foreach (Component c in allComponents)
                {
                    // Check if the target is in the list
                    bool hasBeenAdded = false;
                    foreach (AntiMissileTarget t in Targets)
                    {
                        if (t.Script == c)
                        {
                            hasBeenAdded = true;
                            break;
                        }
                    }

                    if (SupportedTargetTypes.Contains(c.GetType().Name) && !hasBeenAdded)
                    {
                        Targets.Add(new AntiMissileTarget(c));
                        Debug.Log("Added AntiMissileTarget: " + c.ToString());
                    }
                }
            }
        }
        // END Static Update

        if (!InfiniteAmmo && RemainingAmmo <= 0)
        {
            // Skip the search for targets
            LockedTarget = null;
            return;
        }

        // Reload continues regardless of lock status
        TimeToReload -= Time.deltaTime;

        // Called when there is no target or it has been shot down
        if (LockedTarget == null || !LockedTarget.IsDangerous || LockedTarget.Script == null)
        {
            // Reset timing and components
            TimeToTargetDetonation = modifier.DetonateTime;
            LaserLine.enabled = false;

            TimeToNextScan -= Time.deltaTime;
            if (TimeToNextScan <= 0)
            {
                TimeToNextScan = ScanInterval;

                // Don't allow smart alloc to reserve targets when reloading
                if (modifier.SmartAllocMode && TimeToReload >= 0)
                {
                    return;
                }

                // Search for the closest target within the range
                AntiMissileTarget PotentialTarget = null;
                float PotentialTargetDistance = modifier.MaxRange;
                foreach (AntiMissileTarget t in Targets)
                {
                    // Smart allocation mode
                    if (modifier.SmartAllocMode)
                    {
                        bool lockedOnByOtherAmball = false;
                        foreach (AntiMissileBehaviour a in OtherAmballsOnAircraft)
                        {
                            if (a.LockedTarget == t)
                                lockedOnByOtherAmball = true;
                        }

                        if (lockedOnByOtherAmball)
                            continue;
                    }

                    if (t.IsDangerous && t.AircraftObject != ParentAircraftObject)
                    {
                        // Distance check
                        float distance = Vector3.Distance(BallTransform.position, t.TargetTransform.position);
                        if (distance < PotentialTargetDistance)
                        {
                            // Track the locked target by setting target angles
                            Vector3 targetRelativeDirection = transform.InverseTransformDirection(t.TargetTransform.position - transform.position);
                            float targetRelativePitch = Mathf.Atan2(-targetRelativeDirection.y, Mathf.Abs(targetRelativeDirection.z)) * Mathf.Rad2Deg;
                            float targetRelativeYaw = Mathf.Atan2(targetRelativeDirection.x, targetRelativeDirection.z) * Mathf.Rad2Deg;

                            // Don't allow lock if the target is outside the movement range
                            if (targetRelativePitch < -modifier.MaxElevation - modifier.LockAngle ||
                                targetRelativePitch > modifier.MaxDepression + modifier.LockAngle ||
                                targetRelativeYaw < -modifier.MaxRotationLeft - modifier.LockAngle ||
                                targetRelativeYaw > modifier.MaxRotationRight + modifier.LockAngle)
                            {
                                continue;
                            }

                            PotentialTarget = t;
                            PotentialTargetDistance = distance;
                        }
                    }

                    // continue
                }
                LockedTarget = PotentialTarget;
            }
        }
        else
        {
            // Track the locked target by setting target angles
            Vector3 targetRelativeDirection = transform.InverseTransformDirection(LockedTarget.TargetTransform.position - transform.position);
            float targetRelativePitch = Mathf.Atan2(-targetRelativeDirection.y, Mathf.Abs(targetRelativeDirection.z)) * Mathf.Rad2Deg;
            float targetRelativeYaw = Mathf.Atan2(targetRelativeDirection.x, targetRelativeDirection.z) * Mathf.Rad2Deg;

            // Break the lock if the target exits the movement range
            if (targetRelativeDirection.magnitude > modifier.MaxRange ||
                targetRelativePitch < -modifier.MaxElevation - modifier.LockAngle ||
                targetRelativePitch > modifier.MaxDepression + modifier.LockAngle ||
                targetRelativeYaw < -modifier.MaxRotationLeft - modifier.LockAngle ||
                targetRelativeYaw > modifier.MaxRotationRight + modifier.LockAngle)
            {
                LockedTarget = null;
            }

            targetRelativePitch = Mathf.Clamp(targetRelativePitch, -modifier.MaxElevation, modifier.MaxDepression);
            targetRelativeYaw = Mathf.Clamp(targetRelativeYaw, -modifier.MaxRotationLeft, modifier.MaxRotationRight);

            BallPitchAngle = Mathf.MoveTowardsAngle(BallPitchAngle, targetRelativePitch, modifier.VerticalSpeed * Time.deltaTime);
            BallYawAngle = Mathf.MoveTowardsAngle(BallYawAngle, targetRelativeYaw, modifier.HorizontalSpeed * Time.deltaTime);
            BallTransform.localEulerAngles = new Vector3(BallPitchAngle, BallYawAngle, 0f);

            // Used to calculate if the laser can reach the target
            Vector3 boresightDirection = LockedTarget.TargetTransform.position - transform.position;
            float boresightAngle = Vector3.Angle(BallTransform.forward, boresightDirection);
            bool rayBlocked = Physics.Raycast(BallTransform.position, boresightDirection, boresightDirection.magnitude - LockedTarget.ColliderSize, 1 << 20, QueryTriggerInteraction.Ignore);
            bool isLockable = (boresightAngle <= modifier.LockAngle) && !rayBlocked;

            // Reload time will not contribute to detonation time
            if (TimeToReload <= 0)
            {
                // Only count down when target is in lock range
                if (isLockable)
                {
                    TimeToTargetDetonation -= Time.deltaTime;
                }

                if (TimeToTargetDetonation <= modifier.LaserTime && isLockable)
                {
                    LaserLine.SetPositions(new Vector3[] { BallTransform.position, LockedTarget.TargetTransform.position });
                    LaserLine.enabled = true;
                }
                else
                {
                    LaserLine.enabled = false;
                }

                if (TimeToTargetDetonation <= 0)
                {
                    TimeToReload = modifier.ReloadTime;
                    RemainingAmmo--;
                    LockedTarget.Detonate();
                    LockedTarget = null;
                }
            }
        }
    }

    private void PrintLockedTargetInfo()
    {
        if (LockedTarget == null)
        {
            Debug.Log("No target is locked on by this part");
            return;
        }

        StringBuilder builder = new StringBuilder(100);
        builder.AppendLine($"Current target: {LockedTarget.Script}");
        builder.AppendLine($"Transform: {LockedTarget.TargetTransform}");
        builder.AppendLine($"Aircraft: {LockedTarget.AircraftObject}");
        builder.AppendLine($"Collider Size: {LockedTarget.ColliderSize}");
        Debug.Log(builder);
    }

    public class AntiMissileTarget
    {
        // Supported types:
        // AntiAircraftMissileScript MissileScript RocketScript BombScript

        /// <summary>
        /// Component script of a target for anti-missile parts.
        /// </summary>
        public Component Script;

        /// <summary>
        /// Transform of the target.
        /// </summary>
        public Transform TargetTransform;

        /// <summary>
        /// Type of the component script.
        /// </summary>
        public Type ScriptType;

        /// <summary>
        /// Aircraft that the weapon was fired from, or null if not from an aircraft.
        /// </summary>
        public GameObject AircraftObject;

        /// <summary>
        /// The size of the collider calculated using its bounds.
        /// </summary>
        public float ColliderSize = 0f;

        /// <summary>
        /// Whether a target was destroyed by the anti-missile part.
        /// </summary>
        public bool DetonatedByAntiMissile = false;

        /// <summary>
        /// Whether a target has been launched but not detonated.
        /// </summary>
        public bool IsDangerous
        {
            get
            {
                if (DetonatedByAntiMissile || Script == null)
                {
                    return false;
                }
                else if (ScriptType.Name == "AntiAircraftMissileScript")
                {
                    return Script != null;
                }
                else if (ScriptType.Name == "MissileScript" || ScriptType.Name == "BombScript")
                {
                    return (bool)ScriptType.GetProperty("Fired").GetValue(Script) && !(bool)ScriptType.GetProperty("Detonated").GetValue(Script);
                }
                else if (ScriptType.Name == "RocketScript")
                {
                    return (bool)ScriptType.GetProperty("IsLaunched").GetValue(Script) && !(bool)ScriptType.GetProperty("HasExploded").GetValue(Script);
                }
                else
                {
                    return false;
                }
            }
        }

        public void Detonate()
        {
            if (!SupportedTargetTypes.Contains(ScriptType.Name))
            {
                Debug.LogError($"{Script} is not supported and cannot be detonated");
            }
            else
            {
                DetonatedByAntiMissile = true;
            }

            if (ScriptType.Name == "AntiAircraftMissileScript")
            {
                MethodInfo DestroyMissileInfo = ScriptType.GetMethod("DestroyMissileKeepParticleEffects", BindingFlags.NonPublic | BindingFlags.Instance);
                DestroyMissileInfo.Invoke(Script, new object[] { Script.transform.position });
            }
            else if (ScriptType.Name == "MissileScript" || ScriptType.Name == "BombScript")
            {
                TargetTransform.Find("Mesh").gameObject.SetActive(false);
                TargetTransform.Find("Collider").gameObject.SetActive(false);

                MethodInfo DetonateInfo = ScriptType.GetMethod("Detonate");
                DetonateInfo.Invoke(Script, new object[] { Script.transform.position });
            }
            else if (ScriptType.Name == "RocketScript")
            {
                TargetTransform.Find("Mesh").gameObject.SetActive(false);

                if (TargetTransform.Find("Collider") != null)
                    TargetTransform.Find("Collider").gameObject.SetActive(false);

                MethodInfo DetonateInfo = ScriptType.GetMethod("Explode");
                DetonateInfo.Invoke(Script, new object[] { });
            }
        }

        public AntiMissileTarget(Component target)
        {
            Script = target;
            ScriptType = target.GetType();
            TargetTransform = target.transform;

            Component[] parentComponents = target.GetComponentsInParent<Component>();
            foreach (Component c in parentComponents)
            {
                if (c.GetType().Name == "AircraftScript")
                {
                    AircraftObject = c.gameObject;
                    break;
                }
            }
            
            if (ScriptType.Name == "AntiAircraftMissileScript")
            {
                SphereCollider collider = Script.GetComponent<SphereCollider>();
                ColliderSize = collider.radius * 2;
            }
            else
            {
                Collider collider = TargetTransform.Find("Collider").GetComponent<Collider>();
                if (collider == null)
                    ColliderSize = 0f;
                else
                    ColliderSize = collider.bounds.extents.magnitude * 2;
            }
        }

        public override string ToString()
        {
            return Script.ToString();
        }
    }
}