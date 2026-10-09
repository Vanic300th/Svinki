using UnityEngine;

/// <summary>Small store camera, shared by world pickups and the hand-held view.</summary>
public static class PhotoCameraModel
{
    public static GameObject Create(Transform parent, Material shell, Material dark, Material accent)
    {
        var root = new GameObject("Photo camera model"); root.transform.SetParent(parent, false);
        Part("Mint body", root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.34f,.23f,.13f), shell);
        Part("Grip", root.transform, PrimitiveType.Cube, new Vector3(.15f,0,0), new Vector3(.07f,.25f,.16f), dark);
        Part("Lens barrel", root.transform, PrimitiveType.Cylinder, new Vector3(-.025f,0,.105f), new Vector3(.15f,.065f,.15f), dark, new Vector3(90,0,0));
        Part("Lens glass", root.transform, PrimitiveType.Cylinder, new Vector3(-.025f,0,.177f), new Vector3(.105f,.007f,.105f), accent, new Vector3(90,0,0));
        Part("Flash", root.transform, PrimitiveType.Cube, new Vector3(.07f,.078f,.075f), new Vector3(.11f,.038f,.022f), accent);
        Part("Shutter", root.transform, PrimitiveType.Cylinder, new Vector3(.11f,.127f,0), new Vector3(.045f,.011f,.045f), accent);
        Part("Back display", root.transform, PrimitiveType.Cube, new Vector3(-.03f,0,-.073f), new Vector3(.23f,.155f,.013f), dark);
        return root;
    }
    private static void Part(string name,Transform parent,PrimitiveType type,Vector3 pos,Vector3 size,Material material,Vector3 angles=default)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;go.transform.localEulerAngles=angles;
        // Model pieces cannot obstruct the player or a flash ray. The pickup has one root trigger.
        var collider=go.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
        go.GetComponent<Renderer>().sharedMaterial=material;
    }
}
