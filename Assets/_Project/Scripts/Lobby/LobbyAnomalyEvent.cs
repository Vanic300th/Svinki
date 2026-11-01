using UnityEngine;
public abstract class LobbyAnomalyEvent : MonoBehaviour { public virtual int VariantCount => 1; public abstract void Apply(int variant); }
