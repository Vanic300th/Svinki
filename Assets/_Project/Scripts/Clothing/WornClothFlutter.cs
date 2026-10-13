using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Лёгкое колыхание надетой одежды. Меняет только копии сеток в этом экземпляре,
/// поэтому исходный FBX и другие варианты одежды не затрагиваются.
/// </summary>
[DisallowMultipleComponent]
public sealed class WornClothFlutter : MonoBehaviour
{
    private sealed class Piece
    {
        public Mesh Mesh;
        public Vector3[] Rest;
        public Vector3[] Deformed;
        public Vector3[] HolderVertices;
        public float[] Weights;
        public Matrix4x4 HolderToMesh;
    }

    private readonly List<Piece> pieces = new List<Piece>();
    private Transform movementSource;
    private Vector3 previousPosition;
    private Vector3 sway;
    private Vector3 swayVelocity;
    private float amplitude;
    private bool ready;

    public void Initialize(float strength, Transform source)
    {
        movementSource = source != null ? source : transform.root;
        previousPosition = movementSource.position;
        amplitude = Mathf.Max(0f, strength) / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        if (amplitude <= 0f) return;

        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            if (!filter.sharedMesh.isReadable)
            {
                Debug.LogWarning("Для колыхания одежды включите Read/Write у модели: " + filter.sharedMesh.name, filter);
                continue;
            }
            Mesh copy = Instantiate(filter.sharedMesh);
            copy.name = filter.sharedMesh.name + " (Worn Cloth)";
            copy.MarkDynamic();
            filter.sharedMesh = copy;
            AddPiece(copy, filter.transform);
        }

        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            if (!renderer.sharedMesh.isReadable)
            {
                Debug.LogWarning("Для колыхания одежды включите Read/Write у модели: " + renderer.sharedMesh.name, renderer);
                continue;
            }
            Mesh copy = Instantiate(renderer.sharedMesh);
            copy.name = renderer.sharedMesh.name + " (Worn Cloth)";
            copy.MarkDynamic();
            renderer.sharedMesh = copy;
            AddPiece(copy, renderer.transform);
        }

        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float maxX = 0.001f;
        foreach (Piece piece in pieces)
            foreach (Vector3 point in piece.HolderVertices)
            {
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
                maxX = Mathf.Max(maxX, Mathf.Abs(point.x));
            }

        foreach (Piece piece in pieces)
        {
            piece.Weights = new float[piece.Rest.Length];
            for (int i = 0; i < piece.Rest.Length; i++)
            {
                Vector3 point = piece.HolderVertices[i];
                float height = Mathf.InverseLerp(minY, maxY, point.y);
                float hem = Mathf.Clamp01((0.85f - height) / 0.8f);
                hem = hem * hem * (3f - 2f * hem);
                float looseEdge = Mathf.Clamp01(Mathf.Abs(point.x) / maxX) * 0.35f;
                piece.Weights[i] = Mathf.Max(hem, looseEdge);
            }

            Bounds expanded = piece.Mesh.bounds;
            expanded.Expand(amplitude * 5f);
            piece.Mesh.bounds = expanded;
        }

        ready = pieces.Count > 0;
    }

    private void AddPiece(Mesh mesh, Transform meshTransform)
    {
        Vector3[] vertices = mesh.vertices;
        var piece = new Piece
        {
            Mesh = mesh,
            Rest = vertices,
            Deformed = new Vector3[vertices.Length],
            HolderVertices = new Vector3[vertices.Length],
            HolderToMesh = meshTransform.worldToLocalMatrix * transform.localToWorldMatrix
        };
        Matrix4x4 meshToHolder = transform.worldToLocalMatrix * meshTransform.localToWorldMatrix;
        for (int i = 0; i < vertices.Length; i++)
            piece.HolderVertices[i] = meshToHolder.MultiplyPoint3x4(vertices[i]);
        pieces.Add(piece);
    }

    private void LateUpdate()
    {
        if (!ready || movementSource == null) return;
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 currentPosition = movementSource.position;
        Vector3 delta = currentPosition - previousPosition;
        previousPosition = currentPosition;
        Vector3 localVelocity = delta.sqrMagnitude > 4f || deltaTime > 0.2f
            ? Vector3.zero
            : transform.InverseTransformDirection(delta / deltaTime);
        Vector3 targetSway = -new Vector3(localVelocity.x, 0f, localVelocity.z) * amplitude * 0.12f;
        targetSway = Vector3.ClampMagnitude(targetSway, amplitude * 1.5f);
        float step = Mathf.Min(deltaTime, 0.05f);
        swayVelocity += ((targetSway - sway) * 70f - swayVelocity * 8f) * step;
        sway = Vector3.ClampMagnitude(sway + swayVelocity * step, amplitude * 2f);

        float time = Time.time;
        foreach (Piece piece in pieces)
        {
            for (int i = 0; i < piece.Rest.Length; i++)
            {
                Vector3 point = piece.HolderVertices[i];
                float phase = point.x * 5f + point.y * 3f + point.z * 2f;
                float weight = piece.Weights[i];
                Vector3 flutter = new Vector3(
                    Mathf.Sin(time * 5.1f + phase) * amplitude * 0.38f,
                    Mathf.Sin(time * 4.3f + phase * 0.7f) * amplitude * 0.08f,
                    Mathf.Cos(time * 6.2f + phase * 0.8f) * amplitude * 0.45f);
                piece.Deformed[i] = piece.Rest[i] +
                    piece.HolderToMesh.MultiplyVector((sway + flutter) * weight);
            }
            piece.Mesh.vertices = piece.Deformed;
        }
    }

    private void OnDestroy()
    {
        foreach (Piece piece in pieces)
            if (piece.Mesh != null) Destroy(piece.Mesh);
    }
}
