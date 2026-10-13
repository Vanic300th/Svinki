using System;
using UnityEngine;

/// <summary>Editor-baked floor and wall footprints. No second scene camera is rendered.</summary>
public sealed class StoreMap : MonoBehaviour
{
    [Serializable] public struct Footprint { public Vector2 center, size; public float angle; }
    [SerializeField] private Footprint[] floors = Array.Empty<Footprint>();
    [SerializeField] private Footprint[] walls = Array.Empty<Footprint>();
    [SerializeField] private Vector2 minimum, maximum;
    [SerializeField, Range(.1f, 1f)] private float visibleRange = .3f;
    public float VisibleRange => visibleRange;
    public Footprint[] Floors => floors;
    public Footprint[] Walls => walls;
    public Vector2 Minimum => minimum;
    public Vector2 Maximum => maximum;
}
