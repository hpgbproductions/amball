using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Jundroo.SimplePlanes.ModTools.Parts;
using Jundroo.SimplePlanes.ModTools.Parts.Attributes;
using UnityEngine;

/// <summary>
/// A part modifier for SimplePlanes.
/// A part modifier is responsible for attaching a part modifier behaviour script to a game object within a part's hierarchy.
/// </summary>
[Serializable]
public class AntiMissile : Jundroo.SimplePlanes.ModTools.Parts.PartModifier
{
    [SerializeField]
    [DesignerPropertyToggleButton(AllowFunkyInput = false, Label = "Smart Allocation", Order = 0)]
    private bool _smartAlloc = true;

    [SerializeField]
    [DesignerPropertySlider(Header = "Target Tracking", Label = "Max. Traverse Left", MaxValue = 180f, MinValue = 0f, NumberOfSteps = 37, Order = 100)]
    private float _maxRotationLeft = 45f;    // -Y

    [SerializeField]
    [DesignerPropertySlider(Label = "Max. Traverse Right", MaxValue = 180f, MinValue = 0f, NumberOfSteps = 37, Order = 110)]
    private float _maxRotationRight = 45f;    // +Y

    [SerializeField]
    [DesignerPropertySlider(Label = "Max. Elevation", MaxValue = 90f, MinValue = 0f, NumberOfSteps = 19, Order = 120)]
    private float _maxElevation = 45f;    // -X

    [SerializeField]
    [DesignerPropertySlider(Label = "Max. Depression", MaxValue = 90f, MinValue = 0f, NumberOfSteps = 19, Order = 130)]
    private float _maxDepression = 45f;    // +X

    [SerializeField]
    [DesignerPropertySlider(Label = "Horizontal Speed", MaxValue = 1080f, MinValue = 30f, NumberOfSteps = 36, Order = 140)]
    private float _horizontalSpeed = 360f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Vertical Speed", MaxValue = 1080f, MinValue = 30f, NumberOfSteps = 36, Order = 150)]
    private float _verticalSpeed = 360f;

    [SerializeField]
    [DesignerPropertySlider(Header = "Laser Performance", Label = "Lock Angle", MaxValue = 60f, MinValue = 5f, NumberOfSteps = 12, Order = 200)]
    private float _lockAngle = 10f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Max. Range", MaxValue = 6400f, MinValue = 100f, NumberOfSteps = 64, Order = 210)]
    private float _maxRange = 5000f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Total Detonation Time", MaxValue = 5f, MinValue = 0f, NumberOfSteps = 51, Order = 220)]
    private float _detonateTime = 3f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Laser Time", MaxValue = 5f, MinValue = 0f, NumberOfSteps = 51, Order = 230)]
    private float _laserTime = 2f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Reload Time", MaxValue = 5f, MinValue = 0f, NumberOfSteps = 51, Order = 240)]
    private float _reloadTime = 0.5f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Ammo (0 for unlimited)", MaxValue = 15, MinValue = 0, NumberOfSteps = 16, Order = 250)]
    private int _maxAmmo = 8;

    [SerializeField]
    [DesignerPropertySlider(Header = "Laser Appearance", Label = "Red", MaxValue = 1f, MinValue = 0f, NumberOfSteps = 51, Order = 300)]
    private float _colorR = 1f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Green", MaxValue = 1f, MinValue = 0f, NumberOfSteps = 51, Order = 310)]
    private float _colorG = 0f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Blue", MaxValue = 1f, MinValue = 0f, NumberOfSteps = 51, Order = 320)]
    private float _colorB = 0f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Alpha", MaxValue = 1f, MinValue = 0f, NumberOfSteps = 51, Order = 330)]
    private float _colorA = 0.5f;

    [SerializeField]
    [DesignerPropertySlider(Label = "Width", MaxValue = 0.5f, MinValue = 0.01f, NumberOfSteps = 50, Order = 340)]
    private float _laserWidth = 0.1f;

    /// <summary>
    /// If true, the anti missile ball does not lock on to targets that are locked on by others on the same aircraft.
    /// </summary>
    public bool SmartAllocMode
    {
        get { return _smartAlloc; }
    }

    /// <summary>
    /// Maximum rotation in the -Y direction.
    /// </summary>
    public float MaxRotationLeft
    {
        get { return _maxRotationLeft; }
    }

    /// <summary>
    /// Maximum rotation in the +Y direction.
    /// </summary>
    public float MaxRotationRight
    {
        get { return _maxRotationRight; }
    }

    /// <summary>
    /// Maximum rotation in the -X direction.
    /// </summary>
    public float MaxElevation
    {
        get { return _maxElevation; }
    }

    /// <summary>
    /// Maximum rotation in the +X direction.
    /// </summary>
    public float MaxDepression
    {
        get { return _maxDepression; }
    }

    public float HorizontalSpeed
    {
        get { return _horizontalSpeed; }
    }

    public float VerticalSpeed
    {
        get { return _verticalSpeed; }
    }

    public float LockAngle
    {
        get { return _lockAngle; }
    }

    public float MaxRange
    {
        get { return _maxRange; }
    }

    public float DetonateTime
    {
        get { return _detonateTime; }
    }

    public float LaserTime
    {
        get { return _laserTime; }
    }

    public float ReloadTime
    {
        get { return _reloadTime; }
    }

    public int MaxAmmo
    {
        get { return _maxAmmo; }
    }

    public Color LaserColor
    {
        get { return new Color(_colorR, _colorG, _colorB, _colorA); }
    }

    public float LaserWidth
    {
        get { return _laserWidth; }
    }

    /// <summary>
    /// Called when this part modifiers is being initialized as the part game object is being created.
    /// </summary>
    /// <param name="partRootObject">The root game object that has been created for the part.</param>
    /// <returns>The created part modifier behaviour, or <c>null</c> if it was not created.</returns>
    public override Jundroo.SimplePlanes.ModTools.Parts.PartModifierBehaviour Initialize(UnityEngine.GameObject partRootObject)
    {
        // Attach the behaviour to the part's root object.
        var behaviour = partRootObject.AddComponent<AntiMissileBehaviour>();
        return behaviour;
    }
}