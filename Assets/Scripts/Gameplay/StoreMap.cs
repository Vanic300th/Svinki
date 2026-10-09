using System;
using UnityEngine;

/// <summary>Editor-baked floor and wall footprints. No second scene camera is rendered.</summary>
public sealed class StoreMap : MonoBehaviour
{
    [Serializable] public struct Footprint { public Vector2 center, size; public float angle; }
    [SerializeField] private Footprint[] floors = Array.Empty<Footprint>();
    [SerializeField] private Footprint[] walls = Array.Empty<Footprint>();
    [SerializeField] private Vector2 minimum, maximum;
    public Footprint[] Floors => floors;
    public Footprint[] Walls => walls;
    public Vector2 Minimum => minimum;
    public Vector2 Maximum => maximum;
}
